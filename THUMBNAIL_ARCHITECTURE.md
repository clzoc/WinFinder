# WinFinder Thumbnail Architecture

## Goal

WinFinder should not depend on Windows Explorer, installed shell extensions, K-Lite Codec Pack, or the Windows HEIF/HEVC extensions for normal thumbnail coverage. The thumbnail system should become a separate, reusable piece of software that WinFinder can integrate deeply, while still keeping Windows Shell thumbnails as a fallback provider.

The design target is close to macOS Finder and Windows Explorer:

- thumbnails are requested by visible viewport priority, not by full-directory blocking scans;
- decoding happens outside the UI process and cannot freeze the file manager;
- results are cached on disk by file identity and requested size;
- generated thumbnails are shared across app launches;
- unsupported files still get stable, attractive generic icons;
- external integrations can ask for thumbnails without knowing decoder details.

## Current State

The existing pipeline is:

1. `Controls/WorkspacePane.Thumbnails.cs` watches scroll position and marks visible `MyStruct` items as `ThumbLoaded`.
2. Work is executed with `Task.Run` and `Parallel.ForEach`.
3. `ExplorerBackend.GetThumbnail` forwards to `ThumbnailService`.
4. `ThumbnailService` handles a few built-in cases:
   - directories and known generic icons;
   - text previews;
   - PDF first page through bundled `pdfium.dll`;
   - SVG through `SvgThumbnailHelper`;
   - Windows Shell thumbnails through `Microsoft.WindowsAPICodePack.Shell`.

This already has the correct high-level shape: UI scheduling is separate from thumbnail generation. The weak point is that most rich formats still end at `ShellObject.Thumbnail.LargeBitmapSource`, so video, HEIC, camera RAW, Office files, archives, and uncommon image formats depend on whatever the operating system happens to have installed.

## Product Split

Build a separate thumbnail product with three layers:

| Layer | Name | Responsibility |
| --- | --- | --- |
| Client SDK | `WinFinder.Thumbnailing.Client` | Small .NET library used by WinFinder. It owns request objects, result mapping, async calls, cancellation, and fallback policy. |
| Local service | `WinFinder.Thumbnailer.Service` | Long-running local process. It owns scheduling, disk cache, decoder selection, process isolation, telemetry, and health checks. |
| Worker runtime | `WinFinder.Thumbnailer.Worker.Common` | Shared worker infrastructure for JSON protocol formatting, argument parsing, bundled tool discovery, child-process execution, timeouts, and diagnostics. |
| Decoder workers | `WinFinder.Thumbnailer.Worker.*` | Sandboxed worker processes that decode risky or heavy formats, such as video, HEIC, RAW, PDF, SVG, and Office. |

WinFinder should only depend on the client SDK. It should not load FFmpeg, image codecs, Office parsers, or future format-specific libraries into the WPF process.

## Architecture

```text
WorkspacePane
  -> ExplorerBackend
    -> IThumbnailService
      -> ThumbnailClientService
        -> memory cache
        -> local service IPC
          -> disk cache
          -> scheduler
          -> decoder registry
            -> image worker
            -> video worker
            -> document worker
            -> shell fallback worker
```

The existing `IThumbnailService` should remain the integration boundary. The first implementation can replace the current `ThumbnailService` internals with a provider chain:

1. in-memory cache;
2. local thumbnailer client;
3. built-in fast renderers already inside WinFinder, if desired during migration;
4. Windows Shell fallback;
5. generic icon renderer.

Once the service is mature, PDF/SVG/text rendering can move out of the WPF app too.

## IPC Contract

Use a local-only IPC protocol with an explicit version. For this project on Windows, named pipes are a good first choice: no firewall prompts, simple ACLs, and low overhead. HTTP over localhost is easier to debug, but named pipes are better for a desktop helper service.

Request:

```json
{
  "protocolVersion": 1,
  "requestId": "8f2c8b40cbd84a739d6ce75e899dcb5b",
  "path": "D:\\Media\\clip.heic",
  "fileId": {
    "normalizedPath": "D:\\Media\\clip.heic",
    "lastWriteUtcTicks": 638868412300000000,
    "length": 4289912
  },
  "kindHint": "image",
  "extension": ".heic",
  "targetSize": 256,
  "scale": 1.5,
  "mode": "fit",
  "priority": 80,
  "deadlineMs": 4000,
  "allowShellFallback": true
}
```

Response:

```json
{
  "protocolVersion": 1,
  "requestId": "8f2c8b40cbd84a739d6ce75e899dcb5b",
  "status": "ready",
  "source": "cache|generated|shell|generic|unsupported|error",
  "mimeType": "image/png",
  "width": 256,
  "height": 192,
  "cacheKey": "sha256...",
  "bytes": "<binary payload or shared cache path>",
  "diagnostic": null
}
```

For small thumbnails, returning PNG/WebP bytes over the pipe is fine. For larger previews, the service should return a cache file path plus an integrity hash, and the client should load the image with `BitmapCacheOption.OnLoad` so the file is not locked.

## Cache Design

Use two caches.

### Client Memory Cache

WinFinder keeps a small LRU cache of decoded `BitmapSource` objects. This is close to the current `LruBitmapCache` and should remain in-process for smooth scrolling.

Key:

```text
normalizedPath | lastWriteUtcTicks | length | targetSize | scale | renderOptionsVersion
```

### Service Disk Cache

The service owns a persistent disk cache under a per-user app data directory:

```text
%LocalAppData%\\WinFinder\\ThumbnailCache\\v1\\
  00\\sha256.thumb
  00\\sha256.json
  index.sqlite
```

The metadata sidecar or SQLite index stores:

- normalized path;
- volume serial and file index when available;
- last write time and length;
- target size and scale;
- decoder id and decoder version;
- source format;
- creation time and last access time;
- failure code with retry policy.

The disk cache must cache failures too, but with short TTLs. A corrupt video should not be decoded repeatedly while the user scrolls.

## Scheduler

Finder and Explorer feel fast because they prioritize what the user can see. The service scheduler should accept priority and cancellation from the client.

Recommended queue classes:

| Class | Use | Behavior |
| --- | --- | --- |
| Immediate | selected item, preview pane, first visible rows | highest priority, short deadline |
| Visible | current grid/list viewport | normal thumbnail generation |
| Prefetch | near-scroll items | cancellable, low priority |
| Background | folder scan warming | only runs when CPU and disk are idle |

The current `WorkspacePane` scroll timer can map directly to this:

- visible items get `Visible` requests;
- items just above and below the viewport get `Prefetch`;
- when scrolling rapidly, cancel outstanding requests for old viewport generations;
- never set `ThumbLoaded = true` permanently until a real result or terminal fallback is received.

## Decoder Registry

Decoders should be plugins behind a common interface.

```csharp
public interface IThumbnailDecoder {
    string Id { get; }
    Version Version { get; }
    bool CanDecode(ThumbnailDecodeContext context);
    ThumbnailDecodeResult Decode(ThumbnailDecodeContext context);
}
```

The registry chooses by file type, magic bytes, and installed capabilities. Extension alone is not enough.

Recommended first-party decoders:

| Decoder | Formats | Suggested engine |
| --- | --- | --- |
| Image | JPEG, PNG, WebP, GIF, BMP, TIFF, HEIC, AVIF | ImageMagick/Magick.NET, libvips, or WIC plus bundled codecs |
| Video | MP4, MKV, MOV, AVI, WebM, TS, M2TS | bundled FFmpeg libraries or an `ffmpeg` worker |
| PDF | PDF | existing PDFium approach, moved into service |
| SVG | SVG | existing SharpVectors path or a dedicated SVG renderer |
| Text | TXT, MD, JSON, XML, source code | custom renderer with encoding detection |
| Office | DOCX, XLSX, PPTX | embedded preview image first, then document renderer later |
| Archive | ZIP, 7Z, RAR | stable generic icon with optional contents badge |
| ShellFallback | any | Windows Shell thumbnail as last rich fallback |

Do not place all decoders in one process. Video, PDF, SVG, Office, and image decoding all parse complex untrusted files; they should run in worker processes so crashes and memory leaks do not affect the service or WinFinder.

## Video Strategy

Video thumbnails should not depend on DirectShow codecs or K-Lite. Use FFmpeg in a worker process.

Selection rules:

1. Probe duration, streams, rotation, HDR metadata, and keyframes.
2. Avoid frame zero; choose a frame near 10 percent duration, bounded between 1 and 30 seconds.
3. If the frame is black or near-monochrome, retry at 25 percent and 50 percent.
4. Respect rotation metadata.
5. Decode to BGRA/RGBA, resize with high-quality scaling, then encode to PNG or WebP for cache.

This produces Explorer-like behavior while avoiding Explorer's dependency on installed codecs.

## HEIC/AVIF/RAW Strategy

Image thumbnails should not depend on the Windows HEIF Image Extensions. There are two viable approaches:

1. Bundle a native image stack such as libheif/libavif/libraw through a managed wrapper.
2. Use ImageMagick/Magick.NET or libvips with the needed native delegates included in the thumbnailer package.

The service should expose capabilities at startup:

```json
{
  "heic": true,
  "avif": true,
  "raw": false,
  "video": true,
  "pdf": true
}
```

WinFinder can use this for diagnostics, but UI rendering should not branch heavily on capabilities. It should request thumbnails and accept fallback results.

## Shell Integration

There are two separate meanings of "integration":

### Deep Integration With WinFinder

This should be implemented first and does not require administrator rights:

- WinFinder starts the service on demand if it is not running.
- The client SDK sends viewport-prioritized requests.
- Results populate the existing `MyStruct.S0` property.
- The service cache is reused across WinFinder launches.
- A settings page can show decoder health and cache size later.

### Optional Integration With Windows Explorer

Explorer integration should be optional and implemented after the core service is stable:

- register an `IThumbnailProvider` COM shell extension;
- the shell extension should be very thin and call the local service;
- it must have strict timeouts and always return quickly;
- installer must handle per-machine registration and rollback;
- keep this separate from WinFinder's normal app package.

This mirrors how Explorer delegates thumbnails to registered shell providers, but avoids loading heavy decoders into Explorer itself.

## Finder/Explorer Behavior To Copy

Useful behaviors to copy:

- small generic icons appear immediately;
- real thumbnails replace icons asynchronously;
- thumbnails are stable across launches;
- cache invalidates when file content changes;
- visible region beats background work;
- folder icons can use content previews later, but should not block folder navigation;
- bad files fail quietly and do not keep retrying.

Behaviors to avoid:

- decoding on the UI thread;
- relying on global system codecs as the primary path;
- loading native codec libraries into the WPF app;
- marking an item as loaded before the request completes;
- running unbounded parallel thumbnail work during fast scrolling.

## Migration Plan

### Phase 1: Stabilize Current Boundary

- Keep `IThumbnailService`.
- Add an async-friendly result model: `Ready`, `Pending`, `Fallback`, `Error`.
- Change `ThumbLoaded` semantics so temporary failure can retry after cache invalidation.
- Add request generation ids in `WorkspacePane` to ignore stale thumbnails after fast scrolling.
- Keep current PDF/SVG/text/Shell implementation as the fallback provider chain.

### Phase 2: Add Client SDK

- Add `ThumbnailClientService : IThumbnailService`.
- It first checks memory cache, then calls the local service.
- If the service is not installed or fails health check, use the current in-process `ThumbnailService`.
- Add cancellation and request priority.

Current repository status:

- `ThumbnailClientService` is now the default WinFinder thumbnail entry point.
- It owns a client-side decoded bitmap LRU cache, keyed by file identity and target size.
- `ThumbnailRequest` now carries priority, deadline, and cancellation fields so the current viewport scheduler maps cleanly onto the future service scheduler.
- Directory refreshes and search refreshes cancel outstanding thumbnail batches; stale results are still guarded by request ids before touching UI state.
- It performs a short named-pipe health probe against `WinFinder.Thumbnailer.v1`.
- When the service is absent or unhealthy, health checks are throttled and requests fall back to the in-process provider chain.
- `WorkspacePane` now paints an instant placeholder icon for every scheduled item before the pipeline completes: a category-colored folded-page badge icon per extension (image/video/audio/archive/document/code/executable accent colors, white extension label on a bottom band, long extensions shrink instead of truncating), and the folder/archive/ini/binary catalog icons for directories and known static-icon formats. Real thumbnails replace placeholders asynchronously, matching Finder/Explorer tier-1 behavior. Common extension badges are prewarmed in background dispatcher batches, and the badge cache evicts its oldest half instead of clearing wholesale.
- `ThumbnailClientService` resolves directories and static-icon formats locally through `StaticIconCatalog` without a service round trip.

### Phase 3: Build Local Service

- Create `WinFinder.Thumbnailer.Service` as a separate executable.
- Implement named pipe IPC, health endpoint, version negotiation, and disk cache.
- Move PDF, SVG, and text renderers into the service first because they are already known.
- Keep Shell fallback in the service, not in WinFinder.

Current repository status:

- `WinFinder.Thumbnailer.Service` exists as a separate .NET Framework executable in the solution.
- It listens on `WinFinder.Thumbnailer.v1` and implements the versioned health request.
- Health responses now include cache root, cache writability, uptime, and a capabilities object.
- WinFinder's `ThumbnailClientService` starts `Thumbnailer\WinFinder.Thumbnailer.Service.exe` on demand when the first health probe fails.
- The service implements the first real thumbnail request path for text/source-code previews.
- Text thumbnails are rendered in the service, stored as PNG files under `%LocalAppData%\WinFinder\ThumbnailCache\v1\objects`, and returned to WinFinder by cache path.
- PDF first-page thumbnails are now rendered in the service through bundled `pdfium.dll`; the service project copies the native DLL into the `Thumbnailer` output directory and reports PDF capability only when the x64 renderer is available.
- SVG thumbnails are now rendered in the service through SharpVectors. The service uses `FileSvgReader` to produce WPF drawings, rasterizes them on an STA worker thread, and caches the PNG result by file identity and decoder version.
- Windows Shell thumbnail fallback is now also available in the service. It runs after the service-native text/PDF/SVG renderers, writes the shell result into the service disk cache, and keeps WinFinder's in-process shell path only as a migration fallback.
- Renderer failures are cached as short-lived sidecars under the service `failures` directory. A bad or unsupported file no longer forces the same renderer to retry on every scroll pass; the service can skip the recent failure and continue to the next renderer such as Shell fallback.
- Renderer execution now has service-side deadlines with renderer-specific budgets. Fast renderers such as text, SVG, Shell, and PDF keep short waits, while FFmpeg-backed image/video workers get multi-second budgets so first-time generation is not cut off by the generic UI deadline. SVG/Shell still have their own STA render timeouts.
- Unsupported formats still fall back to WinFinder's in-process provider chain.
- A first independent video worker project now exists at `WinFinder.Thumbnailer.Worker.Video`. The service calls it as a separate process, and video capability is enabled only when a bundled `ffmpeg.exe` is present. FFmpeg is packaged once under `Thumbnailer\Tools\ffmpeg`; workers intentionally do not depend on system `PATH`.
- A first independent image worker project now exists at `WinFinder.Thumbnailer.Worker.Image`. It uses the bundled shared FFmpeg tool slot to decode HEIC/HEIF, AVIF, WebP, JPEG XL, and TIFF into PNG cache objects, so HEIC/AVIF thumbnails no longer depend on Windows image extensions.
- Worker projects now share `WinFinder.Thumbnailer.Worker.Common`, which keeps command-line parsing, camelCase JSON serialization, bundled FFmpeg discovery, external process timeouts, async stdout/stderr capture, and first-line diagnostics consistent across image/video workers and future worker types.
- The service side now uses a shared `WorkerProcessClient<TResponse>` to launch worker executables, apply timeouts, capture stdout/stderr asynchronously, parse JSON responses, and normalize diagnostics. Image/video renderers no longer each own their own process management loop.
- `WinFinder.Thumbnailer.Service.exe --diagnose` emits a JSON dependency report covering cache writability, FFmpeg worker status/path/format capabilities, PDFium availability, SharpVectors availability, Shell fallback availability, and aggregate service capabilities.
- Image capability is reported by format, not just by worker availability. The worker probes the bundled FFmpeg build and reports HEIC/HEIF, AVIF, WebP, JPEG XL, TIFF, and RAW separately; the service maps those fields into health/capabilities and keeps RAW false until a RAW-capable worker exists.
- Service-side worker capability checks are cached briefly so health probes do not start FFmpeg workers on every request.
- The service disk cache now enforces a byte budget with LRU eviction. The default budget is 512 MB and can be overridden with `--cache-budget-mb`; `--cache-root` overrides the cache location for portable or test setups. Cache hits refresh the `lastAccessUtcTicks` metadata at most hourly, eviction runs at service startup and after new writes (throttled to every five minutes), and freshly written objects are protected from eviction for ten minutes.
- The named-pipe server now accepts and processes up to four concurrent connections instead of one, so a slow decode no longer blocks every other request behind it. Renderers that need STA or a shared native library (SVG, PDFium) remain internally serialized.
- The client request path uses a separate, longer connect timeout (`RequestConnectTimeoutMs`, 600 ms) than the health probe, so requests queue at the busy service instead of immediately falling back to the in-process chain.
- RAW generation is still pending; it should use the same worker-process pattern with a bundled RAW-capable decoder.

### Phase 4: Add Independent Media Decoders

- Add FFmpeg-based video worker.
- Add HEIC/AVIF-capable image worker.
- Add failure caching and decoder timeout enforcement.
- Add basic diagnostics: decoder id, generation time, cache hit ratio.

### Phase 5: Optional Explorer Extension

- Build a separate COM thumbnail provider.
- The provider calls the service and returns cached thumbnails only within a strict deadline.
- Installer makes this opt-in because shell extension bugs can affect Explorer.

## Implementation Notes For This Repository

The current files map cleanly to the first migration:

- `Backend/ThumbnailModels.cs`: extend request/result models and cache identity.
- `Backend/ThumbnailService.cs`: turn into an orchestrator over provider classes instead of a monolith.
- `Backend/PdfiumThumbnailRenderer.cs`: kept as an in-process fallback during migration; the primary PDF path now also exists in `WinFinder.Thumbnailer.Service`.
- `Utilities/SvgThumbnailHelper.cs`: kept as an in-process fallback during migration; the primary SVG path now also exists in `WinFinder.Thumbnailer.Service`.
- `Controls/WorkspacePane.Thumbnails.cs`: add cancellation/generation tracking and stop treating dispatch as fire-and-forget.
- `WinFinder.csproj`: keep native DLL copying for current app, but new heavy codecs should live in the service project.

Suggested local namespaces:

```text
WinFinder.Backend.Thumbnailing
WinFinder.Thumbnailing.Client
WinFinder.Thumbnailer.Contracts
WinFinder.Thumbnailer.Service
WinFinder.Thumbnailer.Decoders
```

## Minimum Viable Slice

The smallest useful implementation is:

1. named pipe service executable;
2. SQLite or JSON-indexed disk cache;
3. PDF/SVG/text renderers moved behind the service;
4. Shell fallback still available;
5. WinFinder client that can fall back to current `ThumbnailService`.

The first media-focused release should add:

1. FFmpeg worker for video thumbnails;
2. HEIC/AVIF image worker;
3. timeout and crash isolation;
4. diagnostics showing whether a thumbnail came from `cache`, `generated`, `shell`, or `generic`.

That slice directly fixes the K-Lite and HEIC-extension weakness without forcing a full Explorer shell extension or a broad rewrite of WinFinder.

## Packaging And Dependency Closure

The release artifact should be built from a clean package directory, not by copying a previous `bin` tree. Use:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File scripts\package-dist.ps1
```

This creates:

```text
dist\WinFinder\
dist\WinFinder-portable.zip
```

The package includes the WinFinder executable, all copied .NET/NuGet runtime DLLs, the thumbnailer service, image/video workers, PDFium, FFmpeg, and the Everything SDK/runtime slot:

```text
WinFinder.exe
Everything64.dll
Everything\Everything.exe
Thumbnailer\WinFinder.Thumbnailer.Service.exe
Thumbnailer\Tools\ffmpeg\ffmpeg.exe
Thumbnailer\Workers\Image\WinFinder.Thumbnailer.Worker.Image.exe
Thumbnailer\Workers\Video\WinFinder.Thumbnailer.Worker.Video.exe
```

FFmpeg is packaged exactly once under `Thumbnailer\Tools\ffmpeg`. Worker-local FFmpeg copies are treated as a packaging error so stale build output cannot leak into the portable zip.

Everything has two parts:

- `Everything64.dll` is the SDK loaded by WinFinder.
- `Everything\Everything.exe` is the bundled runtime that can provide the IPC server and index.

The Everything index database is not packaged. It is machine/user-specific state and must be generated on the target machine after installation or first launch. Directory browsing therefore uses a normal file-system provider and does not depend on Everything being indexed yet. Global search uses Everything and can start the bundled runtime on demand.

For a portable zip, WinFinder should run after extraction without requiring the user to install external software. Everything can run as a normal bundled process. For a traditional elevated installer, `dist\WinFinder\everything-service.ps1` can install/start the bundled Everything service:

```powershell
powershell -NoProfile -ExecutionPolicy Bypass -File everything-service.ps1 -Action install
```

MSIX packages have stricter service constraints. If MSIX remains a target, keep the portable/on-demand Everything runtime path as the baseline and treat service registration as an installer-specific enhancement for EXE/MSI-style installers.

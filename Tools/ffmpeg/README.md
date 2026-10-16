# FFmpeg Tool Slot

Place the bundled FFmpeg files for thumbnail workers in this directory:

```text
Tools\ffmpeg\ffmpeg.exe
```

If the build is a shared build, keep its required `*.dll` files next to `ffmpeg.exe`. Workers intentionally do not use `ffmpeg.exe` from `PATH`. Media thumbnail capability is reported as unavailable until a bundled executable is present and copied once to:

```text
bin\<platform>\<configuration>\Thumbnailer\Tools\ffmpeg\ffmpeg.exe
```

This keeps video and advanced image thumbnail generation independent from K-Lite, DirectShow codecs, HEIC extensions, and the user's global environment. Do not copy FFmpeg into worker directories; `scripts\package-dist.ps1` treats worker-local FFmpeg binaries as stale packaging errors.

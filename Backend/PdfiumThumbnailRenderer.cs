using System;
using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Windows.Media;
using System.Windows.Media.Imaging;

namespace WinFinder.Backend {
    public interface IPdfThumbnailRenderer {
        BitmapSource RenderFirstPage(string fullPath, int targetPixels);
    }

    public sealed class PdfiumThumbnailRenderer : IPdfThumbnailRenderer {
        private const string PdfiumDllName = "pdfium.dll";
        private const int FpdfAnnot = 0x01;
        private const int FpdfLcdText = 0x02;
        private static readonly object PdfiumSync = new object();
        private static bool initialized;

        public BitmapSource RenderFirstPage(string fullPath, int targetPixels) {
            if (string.IsNullOrWhiteSpace(fullPath) || !File.Exists(fullPath)) {
                return null;
            }

            if (!Environment.Is64BitProcess) {
                Trace.WriteLine("PDF thumbnail rendering requires a 64-bit process because the bundled pdfium.dll is x64.");
                return null;
            }

            try {
                var fileInfo = new FileInfo(fullPath);
                if (fileInfo.Length <= 0 || fileInfo.Length > int.MaxValue) {
                    Trace.WriteLine($"PDF thumbnail skipped for {fullPath}: unsupported file size {fileInfo.Length}.");
                    return null;
                }

                byte[] pdfBytes = File.ReadAllBytes(fullPath);
                int pixelWidth = Math.Max(1, targetPixels <= 0 ? 210 : targetPixels);

                lock (PdfiumSync) {
                    EnsureInitialized();
                    return RenderFirstPageFromBytes(pdfBytes, pixelWidth, fullPath);
                }
            } catch (Exception ex) {
                Trace.WriteLine($"PDF thumbnail failed for {fullPath}: {ex.Message}");
                return null;
            }
        }

        private static void EnsureInitialized() {
            if (initialized) {
                return;
            }

            FPDF_InitLibrary();
            initialized = true;
        }

        private static BitmapSource RenderFirstPageFromBytes(byte[] pdfBytes, int pixelWidth, string tracePath) {
            if (pdfBytes == null || pdfBytes.Length == 0) {
                return null;
            }

            GCHandle documentHandle = default(GCHandle);
            IntPtr document = IntPtr.Zero;
            IntPtr page = IntPtr.Zero;
            IntPtr bitmap = IntPtr.Zero;

            try {
                documentHandle = GCHandle.Alloc(pdfBytes, GCHandleType.Pinned);
                document = FPDF_LoadMemDocument(documentHandle.AddrOfPinnedObject(), pdfBytes.Length, null);
                if (document == IntPtr.Zero) {
                    Trace.WriteLine($"PDFium could not load {tracePath}: {FormatPdfiumError(FPDF_GetLastError())}.");
                    return null;
                }

                if (FPDF_GetPageCount(document) <= 0) {
                    return null;
                }

                page = FPDF_LoadPage(document, 0);
                if (page == IntPtr.Zero) {
                    Trace.WriteLine($"PDFium could not load first page for {tracePath}: {FormatPdfiumError(FPDF_GetLastError())}.");
                    return null;
                }

                double pageWidth = FPDF_GetPageWidth(page);
                double pageHeight = FPDF_GetPageHeight(page);
                if (pageWidth <= 0 || pageHeight <= 0 || double.IsNaN(pageWidth) || double.IsNaN(pageHeight)) {
                    return null;
                }

                int pixelHeight = Math.Max(1, (int)Math.Round(pixelWidth * pageHeight / pageWidth));
                bitmap = FPDFBitmap_Create(pixelWidth, pixelHeight, 1);
                if (bitmap == IntPtr.Zero) {
                    return null;
                }

                FPDFBitmap_FillRect(bitmap, 0, 0, pixelWidth, pixelHeight, 0xFFFFFFFF);
                FPDF_RenderPageBitmap(bitmap, page, 0, 0, pixelWidth, pixelHeight, 0, FpdfAnnot | FpdfLcdText);

                IntPtr buffer = FPDFBitmap_GetBuffer(bitmap);
                int stride = FPDFBitmap_GetStride(bitmap);
                if (buffer == IntPtr.Zero || stride <= 0) {
                    return null;
                }

                byte[] pixels = new byte[stride * pixelHeight];
                Marshal.Copy(buffer, pixels, 0, pixels.Length);

                BitmapSource source = BitmapSource.Create(
                    pixelWidth,
                    pixelHeight,
                    96,
                    96,
                    PixelFormats.Bgra32,
                    null,
                    pixels,
                    stride);

                source.Freeze();
                return source;
            } finally {
                if (bitmap != IntPtr.Zero) {
                    FPDFBitmap_Destroy(bitmap);
                }

                if (page != IntPtr.Zero) {
                    FPDF_ClosePage(page);
                }

                if (document != IntPtr.Zero) {
                    FPDF_CloseDocument(document);
                }

                if (documentHandle.IsAllocated) {
                    documentHandle.Free();
                }
            }
        }

        private static string FormatPdfiumError(uint error) {
            switch (error) {
                case 0:
                    return "success";
                case 1:
                    return "unknown error";
                case 2:
                    return "file access error";
                case 3:
                    return "invalid PDF data";
                case 4:
                    return "unsupported security scheme";
                case 5:
                    return "page error";
                default:
                    return $"error {error}";
            }
        }

        [DllImport(PdfiumDllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void FPDF_InitLibrary();

        [DllImport(PdfiumDllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr FPDF_LoadMemDocument(IntPtr dataBuf, int size, [MarshalAs(UnmanagedType.LPStr)] string password);

        [DllImport(PdfiumDllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void FPDF_CloseDocument(IntPtr document);

        [DllImport(PdfiumDllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int FPDF_GetPageCount(IntPtr document);

        [DllImport(PdfiumDllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr FPDF_LoadPage(IntPtr document, int pageIndex);

        [DllImport(PdfiumDllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void FPDF_ClosePage(IntPtr page);

        [DllImport(PdfiumDllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern double FPDF_GetPageWidth(IntPtr page);

        [DllImport(PdfiumDllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern double FPDF_GetPageHeight(IntPtr page);

        [DllImport(PdfiumDllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr FPDFBitmap_Create(int width, int height, int alpha);

        [DllImport(PdfiumDllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void FPDFBitmap_Destroy(IntPtr bitmap);

        [DllImport(PdfiumDllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void FPDFBitmap_FillRect(IntPtr bitmap, int left, int top, int width, int height, uint color);

        [DllImport(PdfiumDllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern IntPtr FPDFBitmap_GetBuffer(IntPtr bitmap);

        [DllImport(PdfiumDllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern int FPDFBitmap_GetStride(IntPtr bitmap);

        [DllImport(PdfiumDllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern void FPDF_RenderPageBitmap(IntPtr bitmap, IntPtr page, int startX, int startY, int sizeX, int sizeY, int rotate, int flags);

        [DllImport(PdfiumDllName, CallingConvention = CallingConvention.Cdecl)]
        private static extern uint FPDF_GetLastError();
    }
}

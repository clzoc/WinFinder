using System;
using System.Diagnostics;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.Runtime.InteropServices;

namespace WinFinder.Thumbnailer.Service {
    internal sealed class PdfThumbnailRenderer : IThumbnailRenderer {
        private const string PdfiumDllName = "pdfium.dll";
        private const int FpdfAnnot = 0x01;
        private const int FpdfLcdText = 0x02;
        private static readonly object PdfiumSync = new object();
        private static bool initialized;

        public string DecoderId => "pdfium";
        public string DecoderVersion => "1";
        public int RenderTimeoutMs => 1200;

        public static bool IsAvailable {
            get {
                return Environment.Is64BitProcess
                    && File.Exists(Path.Combine(AppDomain.CurrentDomain.BaseDirectory, PdfiumDllName));
            }
        }

        public bool CanRender(ProtocolThumbnailRequest request) {
            return request != null
                && IsAvailable
                && string.Equals(NormalizeFormat(request.Format), "PDF", StringComparison.OrdinalIgnoreCase);
        }

        public Bitmap Render(string path, int targetPixels) {
            if (string.IsNullOrWhiteSpace(path) || !File.Exists(path)) {
                return null;
            }

            if (!IsAvailable) {
                Trace.WriteLine("PDF thumbnail rendering is unavailable because pdfium.dll is missing or the process is not x64.");
                return null;
            }

            try {
                var fileInfo = new FileInfo(path);
                if (fileInfo.Length <= 0 || fileInfo.Length > int.MaxValue) {
                    Trace.WriteLine($"PDF thumbnail skipped for {path}: unsupported file size {fileInfo.Length}.");
                    return null;
                }

                byte[] pdfBytes = File.ReadAllBytes(path);
                int pixelWidth = Math.Max(1, targetPixels <= 0 ? 210 : targetPixels);

                lock (PdfiumSync) {
                    EnsureInitialized();
                    return RenderFirstPageFromBytes(pdfBytes, pixelWidth, path);
                }
            } catch (Exception ex) {
                Trace.WriteLine($"PDF thumbnail failed for {path}: {ex.Message}");
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

        private static Bitmap RenderFirstPageFromBytes(byte[] pdfBytes, int pixelWidth, string tracePath) {
            if (pdfBytes == null || pdfBytes.Length == 0) {
                return null;
            }

            GCHandle documentHandle = default(GCHandle);
            IntPtr document = IntPtr.Zero;
            IntPtr page = IntPtr.Zero;
            IntPtr pdfBitmap = IntPtr.Zero;

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
                pdfBitmap = FPDFBitmap_Create(pixelWidth, pixelHeight, 1);
                if (pdfBitmap == IntPtr.Zero) {
                    return null;
                }

                FPDFBitmap_FillRect(pdfBitmap, 0, 0, pixelWidth, pixelHeight, 0xFFFFFFFF);
                FPDF_RenderPageBitmap(pdfBitmap, page, 0, 0, pixelWidth, pixelHeight, 0, FpdfAnnot | FpdfLcdText);

                IntPtr buffer = FPDFBitmap_GetBuffer(pdfBitmap);
                int stride = FPDFBitmap_GetStride(pdfBitmap);
                if (buffer == IntPtr.Zero || stride <= 0) {
                    return null;
                }

                var bitmap = new Bitmap(pixelWidth, pixelHeight, PixelFormat.Format32bppPArgb);
                BitmapData targetData = null;
                try {
                    targetData = bitmap.LockBits(
                        new Rectangle(0, 0, pixelWidth, pixelHeight),
                        ImageLockMode.WriteOnly,
                        PixelFormat.Format32bppPArgb);

                    CopyBitmapRows(buffer, stride, targetData.Scan0, targetData.Stride, pixelHeight);
                } finally {
                    if (targetData != null) {
                        bitmap.UnlockBits(targetData);
                    }
                }

                return bitmap;
            } finally {
                if (pdfBitmap != IntPtr.Zero) {
                    FPDFBitmap_Destroy(pdfBitmap);
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

        private static void CopyBitmapRows(IntPtr source, int sourceStride, IntPtr target, int targetStride, int height) {
            int rowBytes = Math.Min(Math.Abs(sourceStride), Math.Abs(targetStride));
            byte[] row = new byte[rowBytes];

            for (int y = 0; y < height; y++) {
                IntPtr sourceRow = IntPtr.Add(source, y * sourceStride);
                IntPtr targetRow = IntPtr.Add(target, y * targetStride);
                Marshal.Copy(sourceRow, row, 0, rowBytes);
                Marshal.Copy(row, 0, targetRow, rowBytes);
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

        private static string NormalizeFormat(string format) {
            return (format ?? string.Empty).Trim().TrimStart('.').ToUpperInvariant();
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

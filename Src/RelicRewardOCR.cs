using Avalonia;
using OpenCvSharp;
using Sdcb.PaddleOCR;
using System;
using System.Collections.Generic;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using Rect = OpenCvSharp.Rect;

namespace framenion.Src;

public class RelicRewardOCR
{
	public class Reward
	{
		public required string ItemName;
		public Rect Rect;
	}

	public static List<Reward> ReadRewards(Mat screenshot, PaddleOcrAll engine)
	{
		var rewards = new List<Reward>();
		if (!OperatingSystem.IsWindows()) return rewards;

		//Cv2.ImWrite("debug_screenshot.png", screenshot); // Debug: save full screenshot
		var roiRect = RectFromPercentages(screenshot, 0.25f, 0.20f, 0.50f, 0.24f);
		var roi = screenshot[roiRect];
		//Cv2.ImWrite("roi.png", roi);
		var textRect = new Rect(
				0,
				(int)(roi.Height * 0.65),
				roi.Width,
				(int)(roi.Height * 0.35)
			);

		var textMat = roi[textRect];
		var cardResult = engine.Run(textMat);
		var item = cardResult.Text;

		float GetX1(RotatedRect rect) {
			var pts = rect.Points();
			return pts.Min(p => p.X);
		}

		// Group by x1 (column), threshold in pixels
		float xThreshold = 100f; // adjust as needed for your image/font size

		var words = cardResult.Regions
			.Where(r => !string.IsNullOrWhiteSpace(r.Text))
			.Select(r => (region: r, x1: GetX1(r.Rect)))
			.OrderBy(t => t.x1)
			.ToList();

		var columns = new List<List<PaddleOcrResultRegion>>();

		foreach (var (region, x1) in words) {
			// Try to find a column this word belongs to
			var col = columns.FirstOrDefault(c =>
				Math.Abs(GetX1(c[0].Rect) - x1) < xThreshold);
			if (col == null) {
				col = new List<PaddleOcrResultRegion>();
				columns.Add(col);
			}
			col.Add(region);
		}

		// Optionally, sort each column by Y (top to bottom)
		foreach (var col in columns) {
			col.Sort((a, b) => a.Rect.Center.Y.CompareTo(b.Rect.Center.Y));
		}

		foreach (var col in columns) {
			var itemName = string.Join(" ", col.Select(r => r.Text)).Trim();
			if (!string.IsNullOrEmpty(itemName)) {
				var regionRect = col[0].Rect.BoundingRect();
				int expandedHeight = roiRect.Height;
				var adjustedRect = new Rect(
					regionRect.X + roiRect.X,
					roiRect.Y,
					regionRect.Width,
					expandedHeight
				);
				rewards.Add(new Reward {
					ItemName = itemName,
					Rect = adjustedRect
				});
			}
		}
		return rewards;
	}

	public static Rect RectFromPercentages(
		Mat image,
		float xPct,
		float yPct,
		float widthPct,
		float heightPct)
	{
		xPct = Math.Clamp(xPct, 0f, 1f);
		yPct = Math.Clamp(yPct, 0f, 1f);
		widthPct = Math.Clamp(widthPct, 0f, 1f);
		heightPct = Math.Clamp(heightPct, 0f, 1f);

		int x = (int)(image.Width * xPct);
		int y = (int)(image.Height * yPct);
		int w = (int)(image.Width * widthPct);
		int h = (int)(image.Height * heightPct);

		w = Math.Max(1, Math.Min(w, image.Width - x));
		h = Math.Max(1, Math.Min(h, image.Height - y));

		return new Rect(x, y, w, h);
	}
}

public static class ScreenCapture
{
	#region Win32

	[DllImport("user32.dll")]
	private static extern bool ReleaseDC(IntPtr hWnd, IntPtr hDC);

	[DllImport("gdi32.dll")]
	private static extern IntPtr CreateCompatibleDC(IntPtr hDC);

	[DllImport("gdi32.dll")]
	private static extern bool DeleteDC(IntPtr hDC);

	[DllImport("gdi32.dll")]
	private static extern IntPtr CreateCompatibleBitmap(IntPtr hDC, int width, int height);

	[DllImport("gdi32.dll")]
	private static extern IntPtr SelectObject(IntPtr hDC, IntPtr hObject);

	[DllImport("gdi32.dll")]
	private static extern bool DeleteObject(IntPtr hObject);

	[DllImport("gdi32.dll")]
	private static extern bool BitBlt(
		IntPtr hDestDC,
		int x, int y,
		int width, int height,
		IntPtr hSrcDC,
		int srcX, int srcY,
		CopyPixelOperation rop);

	[DllImport("user32.dll")]
	private static extern IntPtr GetDC(IntPtr hWnd);

	[DllImport("user32.dll")]
	private static extern int GetSystemMetrics(int nIndex);

	private const int SM_CXSCREEN = 0;
	private const int SM_CYSCREEN = 1;

	public static Mat Capture()
	{
		if (!OperatingSystem.IsWindowsVersionAtLeast(6, 1))
			throw new PlatformNotSupportedException("Screen capture is only supported on Windows 7 or later.");
		int width = GetSystemMetrics(SM_CXSCREEN);
		int height = GetSystemMetrics(SM_CYSCREEN);
		var output = new Mat(height, width, MatType.CV_8UC4);

		IntPtr hScreenDC = GetDC(IntPtr.Zero);
		IntPtr hMemDC = CreateCompatibleDC(hScreenDC);
		IntPtr hBitmap = CreateCompatibleBitmap(hScreenDC, width, height);
		IntPtr hOld = SelectObject(hMemDC, hBitmap);

		BitBlt(hMemDC, 0, 0, width, height, hScreenDC, 0, 0, CopyPixelOperation.SourceCopy);

		var bmpData = new BITMAPINFO();
		bmpData.bmiHeader.biSize = Marshal.SizeOf<BITMAPINFOHEADER>();
		bmpData.bmiHeader.biWidth = width;
		bmpData.bmiHeader.biHeight = -height;
		bmpData.bmiHeader.biPlanes = 1;
		bmpData.bmiHeader.biBitCount = 32;
		bmpData.bmiHeader.biCompression = 0;

		GetDIBits(hMemDC, hBitmap, 0, (uint)height, output.Data, ref bmpData, 0);

		// cleanup
		SelectObject(hMemDC, hOld);
		DeleteObject(hBitmap);
		DeleteDC(hMemDC);
		ReleaseDC(IntPtr.Zero, hScreenDC);
		Cv2.CvtColor(output, output, ColorConversionCodes.BGRA2BGR);
		return output;
	}

	[DllImport("gdi32.dll")]
	private static extern int GetDIBits(
		IntPtr hdc,
		IntPtr hbmp,
		uint uStartScan,
		uint cScanLines,
		IntPtr lpvBits,
		ref BITMAPINFO lpbi,
		uint uUsage);

	[StructLayout(LayoutKind.Sequential)]
	private struct BITMAPINFO
	{
		public BITMAPINFOHEADER bmiHeader;
		[MarshalAs(UnmanagedType.ByValArray, SizeConst = 256)]
		public uint[] bmiColors;
	}

	[StructLayout(LayoutKind.Sequential)]
	private struct BITMAPINFOHEADER
	{
		public int biSize;
		public int biWidth;
		public int biHeight;
		public short biPlanes;
		public short biBitCount;
		public int biCompression;
		public int biSizeImage;
		public int biXPelsPerMeter;
		public int biYPelsPerMeter;
		public int biClrUsed;
		public int biClrImportant;
	}

	#endregion
}
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;

namespace KSeF.Services
{
	//Pomocnicza klasa, udostępniająca metodę Windows API sterującą widocznością okna konsoli aplikacji
	internal class ConsoleWindow
	{
		[DllImport("kernel32.dll")]
		private static extern IntPtr GetConsoleWindow();
		[DllImport("user32.dll")]
		private static extern bool ShowWindow(IntPtr hWind, int nCmdShow);
		[DllImport("user32.dll")]
		private static extern bool IsIconic(IntPtr hWnd);
		[DllImport("user32.dll")]
		private static extern bool IsZoomed(IntPtr hWnd);
		[DllImport("user32.dll")]
		private static extern bool IsWindowVisible(IntPtr hWnd);

		public const int SW_HIDE = 0;			//*ukryj okno
		public const int SW_NORMAL = 1;         //*odtwórz okno do oryginalnego rozmiaru [vbNormalFocus]
		public const int SW_NORMALNA = 4;       //*to samo co NORMAL, ale bez aktywacji (focusa) [vbNormalNoFocus]
		public const int SW_SHOW = 5;			// Aktywuje okno (w aktualnym rozmiarze i pozycji)
		public const int SW_SHOWNA = 8;			// To samo co SHOW, ale bez aktywacji (focusa)
		public const int SW_SHOWMIN = 2;        //*Aktywuje okno w postaci zminimalizowanej [vbMinimizedFocus]
		public const int SW_SHOWMINNA = 7;		// To samo co SHOWMIN, ale bez aktywacji
		public const int SW_MINIMIZE = 6;       //*Minimalizuje okno i przekazuje focus do następnego okna [vbMinimizedNoFocus]
		public const int SW_RESTORE = 9;        // Aktywuje i odtwarza oryginalne położenie i rozmiar okna (powrót z MINIMIZE)
		public const int SW_MAXIMIZE = 3;       //*Aktywuje i maksymalizuje okno [vbMaximizedFocus]
		public const int SW_SHOWDEFAULT = 10;	// Ustawia okno tak, jak przy uruchomieniu

		//Zmienia sposób wyświetlania okna konsoli
		//Argumenty:
		//	flag: jedna z flag SW_*, zdefiniowanych powyżej
		public static void SetState(int flag)
		{
			IntPtr hWnd = GetConsoleWindow();
			if (hWnd != IntPtr.Zero)
			{
				ShowWindow(hWnd, flag);
			}
		}
		//Zwraca aktualny stan okna (jedna z flag SW_*), lub -1, gdy nie może tego określić
		public static int GetActualState()
		{
			IntPtr hWnd = GetConsoleWindow();
			if (hWnd == IntPtr.Zero)  return -1;
			if (IsWindowVisible(hWnd))
			{
				if (IsIconic(hWnd)) return SW_MINIMIZE;
				else
					if(IsZoomed(hWnd)) return SW_MAXIMIZE;
				return SW_NORMAL;
			}
			else return SW_HIDE;
		}
	}
}

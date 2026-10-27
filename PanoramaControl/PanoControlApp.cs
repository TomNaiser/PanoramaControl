using System;
using System.Collections.Generic;
using System.Linq;
using System.Windows.Forms;

namespace PanoramaControlApp
{
    static class PanoControlApp
    {
        /// <summary>
        /// Der Haupteinstiegspunkt für die Anwendung.
        /// </summary>
        [STAThread]
        static void Main()
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            try { Application.Run(new PanoControlForm()); }
            catch (DllNotFoundException)
            { MessageBox.Show("PanoramaControl benötigt Windows 10/11 x64 mit winsqlite3.dll und .NET Framework 4.8.", "PanoramaControl", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            catch (System.IO.IOException ex)
            { MessageBox.Show("Das lokale Aufnahmearchiv konnte nicht geöffnet werden. Vorhandene Daten bleiben erhalten.\n\n" + ex.Message, "PanoramaControl", MessageBoxButtons.OK, MessageBoxIcon.Error); }
        }
    }
}

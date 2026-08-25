namespace POTA_Check
{
    internal static class Program
    {
        /// <summary>
        ///  The main entry point for the application.
        /// </summary>
        [STAThread]
        static void Main()
        {
            // Old-style .xls files (like the WWFF/KFF cross reference workbook) can
            // use legacy Windows codepages that .NET doesn't support out of the box.
            // This line unlocks that support - without it, reading an .xls file can
            // throw a "codepage not supported" error the moment "Update WWFF" is used.
            System.Text.Encoding.RegisterProvider(System.Text.CodePagesEncodingProvider.Instance);

            // To customize application configuration such as set high DPI settings or default font,
            // see https://aka.ms/applicationconfiguration.
            ApplicationConfiguration.Initialize();
            Application.Run(new Form1());
        }
    }
}
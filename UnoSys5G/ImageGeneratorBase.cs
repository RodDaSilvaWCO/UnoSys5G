

using System;
using Microsoft.AspNetCore.Components;
using System.IO;

namespace UnoSys5G
{
    public class ImageGeneratorBase : ComponentBase
    {
        //[Parameter]
        //public int Width { get; set; }
        //[Parameter]
        //public int Height { get; set; }
        private int framecounter = 0;
        protected string Image64 { get; set; }

        public void Generate()
        {
            if (framecounter < 2228)
            {
                using (FileStream fs = new FileStream($"C:\\Rodney\\Output\\image_{framecounter++}.png", FileMode.Open, FileAccess.Read, FileShare.None))
                {
                    using (MemoryStream outStream = new MemoryStream())
                    {
                        fs.CopyTo(outStream);
                        Image64 = "data:image/png;base64, " + Convert.ToBase64String(outStream.ToArray());
                    }
                }
            }
            //StateHasChanged();
        }
    }
}



using System.Collections;
using UnityEngine;

namespace SushiParty.Presentation
{
    public static class ThumbnailCapture
    {
        public const int Width = 600;
        public const int Height = 320;

        public static bool IsSupported =>
#if UNITY_EDITOR
            true;
#else
            false;
#endif

        public static IEnumerator CaptureRoutine(string sceneName)
        {
#if UNITY_EDITOR
            yield return new WaitForEndOfFrame();

            Texture2D full = ScreenCapture.CaptureScreenshotAsTexture();
            Texture2D thumbnail = Downscale(full);

            string folder = System.IO.Path.Combine(Application.dataPath, "SushiParty/Resources/Previews");
            System.IO.Directory.CreateDirectory(folder);

            string path = System.IO.Path.Combine(folder, sceneName + ".png");
            System.IO.File.WriteAllBytes(path, thumbnail.EncodeToPNG());

            Object.Destroy(full);
            Object.Destroy(thumbnail);

            UnityEditor.AssetDatabase.Refresh();
            Debug.Log($"[SushiParty] Thumbnail saved: Assets/SushiParty/Resources/Previews/{sceneName}.png");
#else
            yield break;
#endif
        }

        private static Texture2D Downscale(Texture2D source)
        {
            float targetAspect = (float)Width / Height;

            int cropWidth = source.width;
            int cropHeight = Mathf.RoundToInt(source.width / targetAspect);

            if (cropHeight > source.height)
            {
                cropHeight = source.height;
                cropWidth = Mathf.RoundToInt(source.height * targetAspect);
            }

            int originX = (source.width - cropWidth) / 2;
            int originY = (source.height - cropHeight) / 2;

            Texture2D result = new Texture2D(Width, Height, TextureFormat.RGB24, false);

            for (int y = 0; y < Height; y++)
            {
                for (int x = 0; x < Width; x++)
                {
                    float u = (originX + (x + 0.5f) * cropWidth / Width) / source.width;
                    float v = (originY + (y + 0.5f) * cropHeight / Height) / source.height;
                    result.SetPixel(x, y, source.GetPixelBilinear(u, v));
                }
            }

            result.Apply();
            return result;
        }
    }
}

using UnityEngine;
using UnityEngine.UI;

public class ColorBar : MonoBehaviour
{
    private Image colorBarImage;
    private Texture2D colorBarTexture;

    void Start()
    {
        colorBarImage = GetComponent<Image>();
        colorBarTexture = new Texture2D(1, 256); // 縦256pxのテクスチャを生成

        for (int y = 0; y < colorBarTexture.height; y++)
        {
            float t = y / (float)(colorBarTexture.height - 1);
            Color color = Color.Lerp(Color.cyan, Color.red, t); // 0で水色、1で赤色
            colorBarTexture.SetPixel(0, y, color);
        }

        colorBarTexture.Apply(); // テクスチャを適用
        colorBarImage.sprite = Sprite.Create(colorBarTexture, new Rect(0, 0, 1, 256), new Vector2(0.5f, 0.5f));
    }

    void CreateColorBarTexture()
    {
    }
}
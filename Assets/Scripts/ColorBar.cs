using UnityEngine;
using UnityEngine.UI;

public class ColorBar : MonoBehaviour
{
    private Image colorBarImage;
    private Texture2D colorBarTexture;

    void Start()
    {
        colorBarImage = GetComponent<Image>();
        colorBarTexture = new Texture2D(1, 256); // �c256px�̃e�N�X�`���𐶐�

        for (int y = 0; y < colorBarTexture.height; y++)
        {
            float t = y / (float)(colorBarTexture.height - 1);
            Color color = Color.Lerp(Color.cyan, Color.red, t); // 0�Ő��F�A1�ŐԐF
            colorBarTexture.SetPixel(0, y, color);
        }

        colorBarTexture.Apply(); // �e�N�X�`����K�p
        colorBarImage.sprite = Sprite.Create(colorBarTexture, new Rect(0, 0, 1, 256), new Vector2(0.5f, 0.5f));
    }

    void CreateColorBarTexture()
    {
    }
}
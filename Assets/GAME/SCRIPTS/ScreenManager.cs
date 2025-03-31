using UnityEngine;

public class ScreenManager : MonoBehaviour
{
    [Header("Layout Settings")]
    public float minHorizontalPadding = 1.5f;
    public float verticalPadding = 2f;
    public float tubeSpacing = 2f;
    
    [Header("References")]
    public Camera gameCamera;
    public Transform tubesContainer;

    void Start()
    {
        AdjustCameraAndLayout();
        HandleMobileAspect();
    }

    public void AdjustCameraAndLayout()
    {
        // Tính toán dựa trên số lượng ống và kích thước màn hình
        int tubeCount = tubesContainer.childCount;
        float screenRatio = (float)Screen.width / Screen.height;
        
        // Tính toán kích thước camera
        float requiredHorizontalSpace = (tubeCount * tubeSpacing) + (2 * minHorizontalPadding);
        float requiredVerticalSpace = verticalPadding * 2 + 6f; // 6f là chiều cao cần cho các ống
        
        // Chọn orthographicSize dựa trên tỷ lệ màn hình
        float orthoSizeBasedOnWidth = requiredHorizontalSpace / (2 * screenRatio);
        float orthoSizeBasedOnHeight = requiredVerticalSpace / 2;
        
        gameCamera.orthographicSize = Mathf.Max(orthoSizeBasedOnWidth, orthoSizeBasedOnHeight);
        
        // Căn chỉnh vị trí các ống
        ArrangeTubes();
    }

    void ArrangeTubes()
    {
        int tubeCount = tubesContainer.childCount;
        float totalWidth = (tubeCount - 1) * tubeSpacing;
        Vector3 startPos = new Vector3(-totalWidth / 2, 0, 0);
        
        for (int i = 0; i < tubeCount; i++)
        {
            Transform tube = tubesContainer.GetChild(i);
            tube.position = startPos + new Vector3(i * tubeSpacing, 0, 0);
        }
    }

    // Hàm này để test trong Editor
#if UNITY_EDITOR
    void OnValidate()
    {
        if (!Application.isPlaying && gameCamera != null && tubesContainer != null)
        {
            AdjustCameraAndLayout();
        }
    }
#endif
    void HandleMobileAspect()
    {
        // Kiểm tra nếu là mobile
        if (Application.isMobilePlatform || Screen.width < Screen.height)
        {
            // Điều chỉnh lại spacing cho màn hình dọc
            tubeSpacing *= 0.8f;
            verticalPadding *= 1.2f;
        
            // Nếu là màn hình rất hẹp
            if ((float)Screen.height / Screen.width > 2f)
            {
                tubeSpacing *= 0.9f;
                minHorizontalPadding *= 0.8f;
            }
        
            AdjustCameraAndLayout();
        }
    }
}
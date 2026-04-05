using UnityEngine;

public class BigandSmall: MonoBehaviour
{
    [Header("缩放设置")]
    [Tooltip("球体初始大小（默认1倍）")]
    public float initialScale = 1f;

    [Tooltip("球体最大缩放倍数")]
    public float maxScale = 2f;

    [Tooltip("球体最小缩放倍数（建议≥初始大小）")]
    public float minScale = 1f;

    [Tooltip("缩放速度（值越大缩放越快）")]
    public float scaleSpeed = 0.5f;

    // 私有变量：控制缩放状态和计时
    private float currentTime;       
    private bool isScalingUp = true; 

    private void Start()
    {
        transform.localScale = Vector3.one * initialScale;
    }

  
    private void Update()
    {
        currentTime += Time.deltaTime;

        if (isScalingUp)
        {
            transform.localScale = Vector3.MoveTowards(
                transform.localScale,
                Vector3.one * maxScale,
                scaleSpeed * Time.deltaTime
            );

         
            if (currentTime >= 5f)
            {
                isScalingUp = false;
                currentTime = 0f; 
            }
        }
        else
        {
         
            transform.localScale = Vector3.MoveTowards(
                transform.localScale,
                Vector3.one * minScale,
                scaleSpeed * Time.deltaTime
            );

         
            if (currentTime >= 5f)
            {
                isScalingUp = true;
                currentTime = 0f; 
            }
        }
    }

    public void ResetScale()
    {
        transform.localScale = Vector3.one * initialScale;
        currentTime = 0f;
        isScalingUp = true;
    }
}
using UnityEngine;
using UnityEngine.UI;
using TMPro; // 如果使用 TextMeshPro

public class RoomUIManager : MonoBehaviour
{
    public static RoomUIManager Instance;

    [Header("UI 组件绑定")]
    public GameObject roomPanel; // 房间专属UI面板
    public Transform buttonContainer; // 按钮的父节点 (带有 Vertical Layout Group)
    public GameObject clusterButtonPrefab; // 按钮预制体

    private AIIntentController currentAI;

    private void Awake()
    {
        Instance = this;
        roomPanel.SetActive(false);
    }

    public void ShowRoomPanel(TargetZone zoneData, AIIntentController ai)
    {
        currentAI = ai;
        roomPanel.SetActive(true);

        // 清理旧按钮
        foreach (Transform child in buttonContainer)
        {
            Destroy(child.gameObject);
        }

        // 根据房间内的 TargetCluster 动态生成精细化控制按钮
        foreach (var cluster in zoneData.clusters)
        {
            if (cluster != null && !cluster.isCompleted)
            {
                GameObject newBtnObj = Instantiate(clusterButtonPrefab, buttonContainer);

                // 设置按钮文字 (如 "探索资源区", "前往怪物区")
                Text btnText = newBtnObj.GetComponentInChildren<Text>();
                if (btnText != null) btnText.text = $"前往 {cluster.gameObject.name}";

                // 绑定点击事件：点击 UI 按钮相当于玩家点击了对应的 3D 区域
                Button btn = newBtnObj.GetComponent<Button>();
                TargetCluster localCluster = cluster; // 闭包防坑
                btn.onClick.AddListener(() => OnClusterButtonClicked(localCluster));
            }
        }
    }

    public void HideRoomPanel()
    {
        roomPanel.SetActive(false);
    }

    private void OnClusterButtonClicked(TargetCluster cluster)
    {
        if (currentAI != null)
        {
            // 通过 UI 精细化下达指令
            currentAI.CommandToCluster(cluster);
        }
    }
}
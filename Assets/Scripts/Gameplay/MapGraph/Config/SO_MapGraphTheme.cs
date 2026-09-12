using Gameplay.MapGraph.Config;
using TMPro;
using UnityEngine;

namespace Gameplay.MapGraph.Config
{
    /// <summary>指挥地图的统一视觉参数，不保存路线或场景状态。</summary>
    [CreateAssetMenu(menuName="Gameplay/Map Graph/Theme")]
    public sealed class SO_MapGraphTheme : ScriptableObject
    {
        public TMP_FontAsset Font;
        public Color Background=new Color(0.045f,0.061f,0.071f,0.96f);
        public Color ZoneFill=new Color(0.071f,0.091f,0.101f,0.94f);
        public Color Border=new Color(0.22f,0.29f,0.31f,0.65f);
        public Color MutedText=new Color(0.57f,0.65f,0.66f);
        public Color Text=new Color(0.88f,0.93f,0.91f);
        public Color Edge=new Color(0.30f,0.38f,0.39f,0.75f);
        public Color Route=new Color(0.43f,0.77f,0.73f,0.85f);
        public Color FocusedRoute=new Color(0.80f,0.91f,0.84f,0.95f);
        public Color Resource=new Color(0.81f,0.69f,0.43f);
        public Color Enemy=new Color(0.80f,0.42f,0.38f);
        public Color Extraction=new Color(0.43f,0.77f,0.61f);
        public Color Inactive=new Color(0.29f,0.36f,0.37f);
        public float CompactNodeSize=13;
        public float ExpandedNodeSize=24;
        public float CompactAgentSize=12;
        public float ExpandedAgentSize=18;
        public float EdgeWidth=1.25f;
        public Color KindColor(MapGraphNodeKind kind) => kind==MapGraphNodeKind.Resource?Resource :
            kind==MapGraphNodeKind.Extraction?Extraction : kind==MapGraphNodeKind.EnemySource||kind==MapGraphNodeKind.ActiveEnemy?Enemy:Text;
    }
}

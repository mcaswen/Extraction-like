namespace ExtractionLike.Aerospace
{
    /// <summary>Short reading layers; the original catalogue retains the full teaching caveats and sources.</summary>
    internal sealed class AerospaceScienceCopy
    {
        public string system, shortName, question, summary, takeaway;
        public string[] stepTitles, steps;
        public static AerospaceScienceCopy For(string code)
        {
            switch (code)
            {
                case "R01": return new AerospaceScienceCopy {
                    system = "推进系统", shortName = "再生冷却喷管", question = "面对高温燃气，\n喷管如何带走热量？",
                    summary = "喷管让燃气继续膨胀、加速。贴近热壁的冷却通道，则让流经其中的推进剂带走热量，帮助保护结构。",
                    takeaway = "同一股推进剂，也可以先承担冷却任务。",
                    stepTitles = new[] { "沿壁流动", "吸收热量", "继续参与循环" },
                    steps = new[] { "推进剂在进入燃烧区域前，可以流经喷管热壁附近的封闭通道。", "热量穿过壁面，被通道中的流体带走。冷却与喷管轮廓共同服务于发动机工作。", "受热后的推进剂继续进入发动机供给链路。具体路径随发动机循环而变化，本样件只表达基本关系。" } };
                case "R02": return new AerospaceScienceCopy {
                    system = "推进剂供给", shortName = "涡轮泵转子", question = "推进剂怎样送到\n发动机需要的位置？",
                    summary = "泵端的旋转叶轮把能量传给流体，让推进剂继续沿供给链路输送。入口、叶轮和周围壳体共同构成这段路径。",
                    takeaway = "它推动的是内部流体，不是外界空气。",
                    stepTitles = new[] { "入口引入", "叶轮传能", "周围集流" },
                    steps = new[] { "推进剂先到达泵入口。主叶轮前的螺旋诱导轮，是入口部件的一部分。", "旋转叶片向流体传递能量，短轴连接旋转部件，支撑结构维持装配关系。", "流体从叶轮进入周围的扩压或集流区域，再沿出口继续输送。本样件不包含驱动涡轮。" } };
                case "R03": return new AerospaceScienceCopy {
                    system = "推进系统", shortName = "燃烧室喷注器", question = "推进剂进入燃烧室前，\n怎样被有序分配？",
                    summary = "喷注头把来自供给系统的介质分配到许多喷注单元。本件用同轴单元阵列，展示中心通道与外围环隙的关系。",
                    takeaway = "先分路供给，再在相邻出口处相遇。",
                    stepTitles = new[] { "背侧供给", "分配到单元", "面向燃烧室" },
                    steps = new[] { "介质从背侧接口进入各自的供给区域。两路路径保持区分。", "分配区域与面板上的单元相连。放大教学剖面用中心管和外围环隙表达两路通道。", "两股介质到达相邻出口，面向燃烧室。整体内部网络经过简化，不把展示颜色解释为具体推进剂。" } };
                case "R04": return new AerospaceScienceCopy {
                    system = "返回控制", shortName = "可折叠栅格舵", question = "火箭返回时，\n格栅怎样参与控制？",
                    summary = "栅格舵利用大气中的气动力，参与火箭返回阶段的姿态控制。立体格栅、边框和根部连接区共同形成可观察的结构。",
                    takeaway = "需要大气参与；它不是太空中的方向舵。",
                    stepTitles = new[] { "展开舵面", "气流与格栅", "连接到箭体" },
                    steps = new[] { "先观察舵面从收起到展开的过程。折叠动作只展示铰链关系，不模拟完整驱动与锁定。", "开放通道具有实际深度。舵面与大气气流相互作用，通过气动力参与控制；图中的直线箭头不是计算流场。", "边框经根部机构连接箭体。栅格舵需要大气参与，这种控制作用不能直接套用到真空环境。" } };
                default: return new AerospaceScienceCopy {
                    system = "载荷接口", shortName = "载荷分离机构", question = "一直牢固连接的载荷，\n怎样与火箭分开？",
                    summary = "分离机构在需要连接时保持约束，随后解除约束，让载荷与运载器建立相对运动。这个弧段展示上下接口与夹紧区域的关系。",
                    takeaway = "解除约束和建立分离运动，是两个概念。",
                    stepTitles = new[] { "保持连接", "解除约束", "建立相对运动" },
                    steps = new[] { "上下接口由夹紧区域约束。完整接口围绕一整圈，本件只取其中一段。", "释放系统使约束解除。这里仅展示外壳与相邻结构，不复原装置内部。", "某些系统用弹簧推力元件帮助建立相对运动。界面的位移只表达关系，不模拟实际速度、时序或储能。" } };
            }
        }
    }
}

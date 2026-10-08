namespace ExtractionLike.Aerospace
{
    /// <summary>Component overview and principle copy shared by the five dossier pages.</summary>
    internal sealed class AerospaceScienceCopy
    {
        public string system, shortName, question, summary, takeaway, displayTitle, headline, principleTitle;
        public string[] stepTitles, steps;
        public static AerospaceScienceCopy For(string code)
        {
            switch (code)
            {
                case "R01": return new AerospaceScienceCopy {
                    system = "推进系统", shortName = "再生冷却喷管", question = "面对高温燃气，\n喷管如何带走热量？",
                    displayTitle = "再生冷却\n喷管组件", headline = "让燃气加速，也让热量离开。", principleTitle = "先带走热量，再继续供给。",
                    summary = "喷管让燃气继续膨胀、加速。贴近热壁的冷却通道，则让流经其中的推进剂带走热量，帮助保护结构。",
                    takeaway = "同一股推进剂，也可以先承担冷却任务。",
                    stepTitles = new[] { "沿壁流动", "吸收热量", "继续参与循环" },
                    steps = new[] { "推进剂可以先流经喷管热壁附近的封闭通道。", "热量穿过壁面，由通道中的流体带走，帮助保护结构。", "受热后的推进剂继续进入发动机供给链路，具体路径随发动机循环而变化。" } };
                case "R02": return new AerospaceScienceCopy {
                    system = "推进剂供给", shortName = "涡轮泵转子", question = "推进剂怎样送到\n发动机需要的位置？",
                    displayTitle = "涡轮泵\n转子组件", headline = "把旋转的能量，传给推进剂。", principleTitle = "从入口到叶轮，再到出口。",
                    summary = "泵端的旋转叶轮把能量传给流体，让推进剂继续沿供给链路输送。入口、叶轮和周围壳体共同构成这段路径。",
                    takeaway = "它推动的是内部流体，不是外界空气。",
                    stepTitles = new[] { "入口引入", "叶轮传能", "周围集流" },
                    steps = new[] { "推进剂先到达泵入口，经过主叶轮前方的螺旋诱导轮。", "旋转叶片向流体传递能量，轴传递转矩，支撑结构维持旋转部件的位置。", "流体进入叶轮周围的扩压或集流区域，再沿出口进入后续供给链路。" } };
                case "R03": return new AerospaceScienceCopy {
                    system = "推进系统", shortName = "燃烧室喷注器", question = "推进剂进入燃烧室前，\n怎样被有序分配？",
                    displayTitle = "燃烧室\n喷注器组件", headline = "分路供给，在出口相遇。", principleTitle = "从供给区域，到喷注单元。",
                    summary = "喷注头把来自供给系统的介质分配到许多喷注单元。同轴单元中的中心通道与外围环隙，让两路介质到达相邻出口。",
                    takeaway = "先分路供给，再在相邻出口处相遇。",
                    stepTitles = new[] { "背侧供给", "分配到单元", "面向燃烧室" },
                    steps = new[] { "介质从背侧接口进入各自的供给区域，两路路径保持独立。", "分配区域与面板上的单元相连，中心管和外围环隙形成两路通道。", "两股介质从相邻出口进入燃烧室，喷注方式影响后续的混合过程。" } };
                case "R04": return new AerospaceScienceCopy {
                    system = "返回控制", shortName = "可折叠栅格舵", question = "火箭返回时，\n格栅怎样参与控制？",
                    displayTitle = "可折叠\n栅格舵组件", headline = "借助气流，调整返回姿态。", principleTitle = "展开舵面，让气流参与控制。",
                    summary = "栅格舵利用大气中的气动力，参与火箭返回阶段的姿态控制。立体格栅、边框和根部连接区共同形成可观察的结构。",
                    takeaway = "需要大气参与；它不是太空中的方向舵。",
                    stepTitles = new[] { "展开舵面", "气流与格栅", "连接到箭体" },
                    steps = new[] { "舵面绕根部铰链从收起位置展开，进入工作位置。", "气流穿过立体格栅，与舵面相互作用，产生参与姿态控制的气动力。", "边框经根部机构连接箭体并传递载荷，栅格舵的控制作用需要大气参与。" } };
                default: return new AerospaceScienceCopy {
                    system = "载荷接口", shortName = "载荷分离机构", question = "一直牢固连接的载荷，\n怎样与火箭分开？",
                    displayTitle = "载荷分离\n机构弧段", headline = "连接时牢固，分离时有序。", principleTitle = "先解除约束，再建立运动。",
                    summary = "分离机构在需要连接时保持约束，随后解除约束，让载荷与运载器建立相对运动。这个弧段展示上下接口与夹紧区域的关系。",
                    takeaway = "解除约束和建立分离运动，是两个概念。",
                    stepTitles = new[] { "保持连接", "解除约束", "建立相对运动" },
                    steps = new[] { "夹紧带与夹块将上下接口保持在一起，形成连接载荷与运载器的环形接口。", "释放系统解除夹紧约束，使上下接口具备分离条件。", "某些系统利用弹簧推力元件，在解除约束后帮助载荷与运载器建立相对运动。" } };
            }
        }
    }
}

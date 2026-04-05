using System;
using BoardGame.Runtime;
using UnityEngine;

namespace BoardGame.Runtime.State
{
    /// <summary>
    /// 单次升级可选的一项增益
    /// </summary>
    [Serializable]
    public sealed class BoardLevelUpChoice
    {
        [SerializeField] private BoardLevelUpBuffType _buffType = BoardLevelUpBuffType.AttackFlat;
        [SerializeField] private int _value = 1;

        public BoardLevelUpChoice(BoardLevelUpBuffType buffType, int value)
        {
            _buffType = buffType;
            _value = Mathf.Max(1, value);
        }

        public BoardLevelUpBuffType BuffType => _buffType;
        public int Value => _value;
        public string DisplayLabel => $"+{_value} {BoardGameTypes.GetLevelUpBuffLabel(_buffType)}";
    }
}

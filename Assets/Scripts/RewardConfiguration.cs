using System;
using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(fileName = "RewardConfiguration", menuName = "Scriptable Objects/RewardConfiguration")]
public class RewardConfiguration : ScriptableObject
{
    [Header("Reward Configuration")]
    [field: SerializeField] public RewardConfig[] RewardConfig { get; private set; }
}

[Serializable]
public struct RewardConfig {
    public RewardType type;
    public float value;
}

public enum RewardType
{
    BallHitTarget,
    BallHitDrain,
    BallHitResetZone,
    DistBallTargetDown,
    DistBallTargetUp,
    ShootWithBall,
    ShootWithoutBall,
    BallWentHigher,
    BallStayStill,
    FlipperStaysUp
}
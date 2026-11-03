using System;
using System.Collections.Generic;
using UnityEngine;
using Object = UnityEngine.Object;

using System.IO;
using System.Text;
using System.Globalization;
using System.Linq;

public class RewardManager : MonoBehaviour
{
    public static RewardManager Instance;
    
    [SerializeField] private RewardConfiguration _rewardConfig;
    private Dictionary<RewardType, float> _rewardPoints;
    private Dictionary<FlipperAgent, AgentReward> _agentRewards;
    private FlipperAgent[] _agents;
    private float _allAgentsRewards = 0f;
    
    private void Awake()
    {
        if(Instance == null)
        {
            Instance = this;
        }
        else
        {
            Debug.LogWarning("There is more than one RewardManager.");
            Destroy(gameObject);
        }
    }

    private void Start()
    {
        if (_rewardConfig != null)
        {
            _rewardPoints = InitializeRewardPoints(_rewardConfig.RewardConfig);
        }
        
        _agents = Object.FindObjectsByType<FlipperAgent>();
        _agentRewards = InitializeAgents(_agents);
    }
    
    
    private Dictionary<RewardType, float> InitializeRewardPoints(RewardConfig[] array)
    {
        if (array == null) { return new Dictionary<RewardType, float>(); }
        
        Dictionary<RewardType, float> result = new Dictionary<RewardType, float>();

        foreach (RewardConfig reward in array)
        {
            result[reward.type] = reward.value;
        }

        if (result.Keys.Count != Enum.GetNames(typeof(RewardType)).Length)
        {
            Debug.LogWarning("Reward configuration doesn't define all reward points");
        }
        
        return result;
    }
    
    public Dictionary<RewardType, int> InitializeRewardType()
    {
        Dictionary<RewardType, int> result = new Dictionary<RewardType, int>();
        
        foreach (RewardType type in Enum.GetValues(typeof(RewardType)))
        {
            result[type] = 0;
        }
        
        return result;
    }

    private Dictionary<FlipperAgent, AgentReward> InitializeAgents(FlipperAgent[] agents)
    {
        Dictionary<FlipperAgent, AgentReward> result = new Dictionary<FlipperAgent, AgentReward>();
        
        foreach (FlipperAgent agent in agents)
        {
            result[agent] = new AgentReward(agent.Id);
        }
        
        return result;
    }

    

    public void AddReward(FlipperAgent agent, RewardType type)
    {
        if (type == RewardType.DistBallTargetDown)
        {
            OnDistBallTargetDown(agent);
            return;
        }
        
        Debug.Log("add reward du manager :" + $"{type}");
        agent.AddReward(_rewardPoints[type]); 
        _allAgentsRewards += _rewardPoints[type];

        AgentReward temp = _agentRewards[agent];
        temp.cumulatedRewards += _rewardPoints[type];
        temp.rewardTypeCounters[type] += 1;
        temp.rewardCount += 1f;
        temp.avgRewards = temp.cumulatedRewards / temp.rewardCount;
        _agentRewards[agent] = temp;
    }

    private void OnDistBallTargetDown(FlipperAgent agent)
    {
        float distance = agent.OriginalDistBallTarget - agent.MinDistBallTarget;
        float advancement = distance / agent.OriginalDistBallTarget;
        float reward = Mathf.Lerp(0f, _rewardPoints[RewardType.DistBallTargetDown], advancement);
        float value = reward - agent.currentDistReward;
        agent.currentDistReward =  reward;
        
        Debug.Log("add reward du manager :" + $"{RewardType.DistBallTargetDown}");
        agent.AddReward(value);
        _allAgentsRewards += value;
        
        AgentReward temp = _agentRewards[agent];
        temp.cumulatedRewards += value;
        temp.rewardTypeCounters[RewardType.DistBallTargetDown] += 1;
        temp.rewardCount += 1f;
        temp.avgRewards = temp.cumulatedRewards / temp.rewardCount;
        _agentRewards[agent] = temp;
    }

    

    public void UpdateAvgRewardsEpisode(FlipperAgent agent)
    {
        AgentReward temp = _agentRewards[agent];
        temp.episodeCount += 1f;
        temp.avgRewardsEpisode = temp.cumulatedRewards / temp.episodeCount;
        _agentRewards[agent] = temp;
    }
    
    
    public void ExportToCsv(string path = null)
    {
        path ??= Path.Combine(Application.persistentDataPath, "rewards.csv");

        RewardType[] types = (RewardType[])Enum.GetValues(typeof(RewardType));
        StringBuilder sb = new StringBuilder();

        //Catégories
        sb.Append("id,cumulatedRewards,avgRewards,avgRewardsEpisode");
        foreach (RewardType type in types)
            sb.Append(',').Append(type);
        sb.AppendLine();

        //Une ligne un agent
        foreach (AgentReward r in _agentRewards.Values.OrderBy(a => a.id))
        {
            sb.Append(r.id.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(r.cumulatedRewards.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(r.avgRewards.ToString(CultureInfo.InvariantCulture)).Append(',')
                .Append(r.avgRewardsEpisode.ToString(CultureInfo.InvariantCulture));

            foreach (RewardType type in types)
            {
                r.rewardTypeCounters.TryGetValue(type, out int count);
                sb.Append(',').Append(count);
            }

            sb.AppendLine();
        }

        File.WriteAllText(path, sb.ToString());
        Debug.Log($"Rewards exported to {path}");
    }
    
    //On exporte en csv à chaque fin d'une scène
    private void OnDestroy()
    {
        if (Instance != this) return;
        ExportToCsv();
    }
}

public struct AgentReward
{
    public int id;
    
    public float cumulatedRewards;
    public float avgRewards;
    public float rewardCount;
    
    public float avgRewardsEpisode;
    public float episodeCount;

    public Dictionary<RewardType, int> rewardTypeCounters;

    public AgentReward(int agentId)
    {
        id = agentId;
        cumulatedRewards = 0f;
        avgRewards = 0f;
        rewardCount = 0f;
        avgRewardsEpisode = 0f;
        episodeCount = 0f;
        rewardTypeCounters = RewardManager.Instance.InitializeRewardType();
    }
}


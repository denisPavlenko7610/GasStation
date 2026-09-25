using GasStation.Components;
using GasStation.Logic;
using Unity.Burst;
using Unity.Entities;

namespace GasStation.Systems
{
    /// <summary>
    /// Runs the "Inheritance" campaign: a chapter is done as soon as its goal is met; missing a deadline hands
    /// the station to the bank. After the third chapter the game goes on as free play.
    /// </summary>
    [BurstCompile]
    [UpdateInGroup(typeof(StationSystemGroup), OrderLast = true)]
    [UpdateBefore(typeof(QuestSystem))]
    public partial struct CampaignSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<Campaign>();
            state.RequireForUpdate<GameTime>();
            state.RequireForUpdate<StationEvent>();
        }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            ref var campaign = ref SystemAPI.GetSingletonRW<Campaign>().ValueRW;
            if (!campaign.Active)
                return;

            var events = SystemAPI.GetSingletonBuffer<StationEvent>();
            int day = SystemAPI.GetSingleton<GameTime>().Day;
            int level = SystemAPI.HasSingleton<StationLevel>() ? SystemAPI.GetSingleton<StationLevel>().Level : 1;
            var goal = CampaignMath.Goal(campaign.Chapter);

            if (CampaignMath.Met(goal, campaign.Repaid, level))
            {
                if (campaign.Chapter >= CampaignMath.Chapters)
                {
                    campaign.Active = false;
                    campaign.Outcome = CampaignOutcome.Won;
                    StationEvent.Push(events, StationEventType.CampaignWon);
                    return;
                }

                campaign.Chapter++;
                StationEvent.Push(events, StationEventType.CampaignChapter, default, campaign.Chapter);
                return;
            }

            if (CampaignMath.Missed(goal, day))
            {
                campaign.Active = false;
                campaign.Outcome = CampaignOutcome.Lost;
                StationEvent.Push(events, StationEventType.GameOver, default, 0f, (int)CampaignOutcome.Lost);
            }
        }
    }
}

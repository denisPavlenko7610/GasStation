using GasStation.Bridge;
using GasStation.Components;
using GasStation.Localization;
using GasStation.Logic;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace GasStation.Systems
{
    /// <summary>Turns this frame's station events into HUD messages, reviews and sounds (HudModel.Events).</summary>
    public partial class HudBridgeSystem
    {
        private void DrainEvents()
        {
            var events = SystemAPI.GetSingletonBuffer<StationEvent>();
            float weeklyBills = 0f;
            for (int i = 0; i < events.Length; i++)
            {
                var stationEvent = events[i];
                HudModel.Events.Add(stationEvent);

                string fuel = GameTexts.FuelName(stationEvent.Fuel);
                switch (stationEvent.Type)
                {
                    case StationEventType.CustomerLeftAngry:
                        HudModel.Notify(Loc.T("msg.customerAngry"));
                        break;
                    case StationEventType.FuelRanOut:
                        HudModel.Notify(Loc.F("msg.fuelRanOut", fuel));
                        break;
                    case StationEventType.FuelDelivered:
                        HudModel.Notify(Loc.F("msg.fuelDelivered", stationEvent.Value, fuel));
                        break;
                    case StationEventType.PumpBroken:
                        HudModel.Notify(Loc.F("msg.pumpBroken", stationEvent.Value));
                        break;
                    case StationEventType.PumpRepaired:
                        HudModel.Notify(Loc.F("msg.pumpRepaired", stationEvent.Value));
                        break;
                    case StationEventType.NotEnoughMoney:
                        HudModel.Notify(Loc.F("msg.noMoney", stationEvent.Value));
                        break;
                    case StationEventType.LevelUp:
                        HudModel.Notify(Loc.F("msg.levelUp", stationEvent.Value));
                        break;
                    case StationEventType.TipReceived:
                        HudModel.Notify(Loc.F("msg.tip", stationEvent.Value));
                        break;
                    case StationEventType.FuelStolen:
                        HudModel.Notify(Loc.F("msg.fuelStolen", stationEvent.Value));
                        break;
                    case StationEventType.ThiefCaught:
                        HudModel.Notify(Loc.T("msg.thiefCaught"));
                        break;
                    case StationEventType.MarketChanged:
                        HudModel.Notify(Loc.F("msg.market", stationEvent.Value));
                        break;
                    case StationEventType.Vandals:
                        HudModel.Notify(Loc.T("msg.vandals"));
                        break;
                    case StationEventType.RushHourStarted:
                        HudModel.Notify(Loc.T("msg.rushHour"));
                        break;
                    case StationEventType.SandstormStarted:
                        HudModel.Notify(Loc.T("msg.sandstorm"));
                        break;
                    case StationEventType.InspectionPassed:
                        HudModel.Notify(Loc.F("msg.inspectionPassed", stationEvent.Value));
                        break;
                    case StationEventType.ShopEmpty:
                        HudModel.Notify(Loc.T("msg.shopEmpty"));
                        break;
                    case StationEventType.RestroomDisgusting:
                        HudModel.Notify(Loc.T("msg.restroomDisgusting"));
                        break;
                    case StationEventType.WorkerStole:
                        HudModel.Notify(Loc.F("msg.workerStole", stationEvent.Value));
                        break;
                    case StationEventType.AchievementUnlocked:
                        HudModel.Notify(Loc.F("msg.achievement", Loc.T($"achievement.{(AchievementId)(int)stationEvent.Value}.name")));
                        break;
                    case StationEventType.CustomerReview:
                        HudModel.Reviews.Insert(0, new Review
                        {
                            Stars = (int)stationEvent.Value,
                            Variant = UnityEngine.Random.Range(0, ReviewMath.VariantsPerStar),
                            Day = HudModel.Day,
                            Hour = HudModel.Hour
                        });
                        if (HudModel.Reviews.Count > HudModel.MaxReviews)
                            HudModel.Reviews.RemoveAt(HudModel.Reviews.Count - 1);
                        break;
                    case StationEventType.TruckArrived:
                        HudModel.Notify(Loc.T(stationEvent.Value > 0.5f ? "msg.tankerArrived" : "msg.cargoArrived"));
                        break;
                    case StationEventType.RenovationDone:
                        HudModel.Notify(Loc.F("msg.renovated", Loc.T($"renovation.{(RenovationKind)(int)stationEvent.Value}")));
                        break;
                    case StationEventType.RenovationNeedsLevel:
                        HudModel.Notify(Loc.F("msg.renovationNeedsLevel", stationEvent.Value));
                        break;
                    case StationEventType.MotelPaid:
                        HudModel.Notify(Loc.F("msg.motelPaid", stationEvent.Value));
                        break;
                    case StationEventType.TruckParked:
                        HudModel.Notify(Loc.T("msg.truckParked"));
                        break;
                    case StationEventType.ParkingPaid:
                        HudModel.Notify(Loc.F("msg.parkingPaid", stationEvent.Value));
                        break;
                    case StationEventType.ProductsDelivered:
                        HudModel.Notify(Loc.F("msg.productsDelivered", stationEvent.Value));
                        break;
                    case StationEventType.InspectionFailed:
                        HudModel.Notify(Loc.F("msg.inspectionFailed", stationEvent.Value));
                        break;
                    case StationEventType.UtilitiesPaid:
                        HudModel.LastUtilities = stationEvent.Value;
                        break;
                    case StationEventType.TaxPaid:
                    case StationEventType.LoanPayment:
                    case StationEventType.InsurancePremiumPaid:
                        weeklyBills += stationEvent.Value;
                        break;
                    case StationEventType.LoanRepaid:
                        if (stationEvent.Value <= 0f)
                            HudModel.Notify(Loc.T("msg.loanPaidOff"));
                        break;
                    case StationEventType.InsurancePayout:
                        HudModel.Notify(Loc.F("msg.insurancePayout", stationEvent.Value));
                        break;
                    case StationEventType.BankruptcyWarning:
                        HudModel.Notify(Loc.F("msg.bankruptcyWarning", stationEvent.Value));
                        break;
                    case StationEventType.CompetitorOpened:
                        HudModel.Notify(Loc.T("msg.competitorOpened"));
                        break;
                    case StationEventType.CompetitorPriceCut:
                        HudModel.Notify(Loc.T("msg.competitorPriceCut"));
                        break;
                    case StationEventType.CompetitorPriceRise:
                        HudModel.Notify(Loc.T("msg.competitorPriceRise"));
                        break;
                    case StationEventType.CompetitorPromoStarted:
                        HudModel.Notify(Loc.T($"msg.competitorPromo.{(CompetitorPromo)(int)stationEvent.Value}"));
                        break;
                    case StationEventType.RegularArrived:
                        HudModel.Notify(stationEvent.Value > 0.5f
                            ? Loc.F("msg.regularMet", GameTexts.RegularName(stationEvent.Subject), GameTexts.RegularAbout(stationEvent.Subject))
                            : Loc.F("msg.regularArrived", GameTexts.RegularName(stationEvent.Subject),
                                Loc.T($"regular.{stationEvent.Subject - 1}.line.{UnityEngine.Random.Range(0, 2)}")));
                        break;
                    case StationEventType.RegularVisit:
                        // The review pushed just before belongs to this regular.
                        if (HudModel.Reviews.Count > 0)
                        {
                            var review = HudModel.Reviews[0];
                            review.RegularId = stationEvent.Subject;
                            HudModel.Reviews[0] = review;
                        }
                        break;
                    case StationEventType.RegularLost:
                        HudModel.Notify(Loc.F(HudModel.Competitor.Active && !HudModel.Competitor.BoughtOut ? "msg.regularLost" : "msg.regularLostNoRival",
                            GameTexts.RegularName(stationEvent.Subject)));
                        break;
                    case StationEventType.RegularBestFriend:
                        HudModel.Notify(Loc.F("msg.regularFriend", GameTexts.RegularName(stationEvent.Subject)));
                        break;
                    case StationEventType.CriticArticle:
                        HudModel.Notify(Loc.T(stationEvent.Value > 1f ? "msg.criticPraise" : "msg.criticPan"));
                        break;
                    case StationEventType.SpecialArrived:
                        HudModel.Notify(Loc.F($"msg.special.{(CustomerType)(int)stationEvent.Value}", stationEvent.Subject));
                        break;
                    case StationEventType.EmergencyServed:
                        HudModel.Notify(Loc.T("msg.emergencyServed"));
                        break;
                    case StationEventType.ContractOffered:
                        HudModel.Notify(Loc.F("msg.contractOffer", GameTexts.ContractName((ContractType)(int)stationEvent.Value)));
                        break;
                    case StationEventType.ContractAccepted:
                        HudModel.Notify(Loc.F("msg.contractAccepted", GameTexts.ContractName((ContractType)(int)stationEvent.Value)));
                        break;
                    case StationEventType.ContractPenalty:
                        HudModel.Notify(Loc.F("msg.contractPenalty", stationEvent.Value));
                        break;
                    case StationEventType.ContractCompleted:
                        HudModel.Notify(Loc.F("msg.contractCompleted", stationEvent.Value));
                        break;
                    case StationEventType.ContractCancelled:
                        HudModel.Notify(Loc.F("msg.contractCancelled", GameTexts.ContractName((ContractType)(int)stationEvent.Value)));
                        break;
                    case StationEventType.WorkerQuit:
                        HudModel.Notify(Loc.F("msg.workerQuit", GameTexts.RoleName((StaffRole)(int)stationEvent.Value),
                            GameTexts.StaffName(stationEvent.Subject)));
                        break;
                    case StationEventType.ProductsSpoiled:
                        HudModel.Notify(Loc.F("msg.spoiled", stationEvent.Value, GameTexts.ProductName((ProductType)stationEvent.Subject)));
                        break;
                    case StationEventType.ProductsShort:
                        HudModel.Notify(Loc.F("msg.short", stationEvent.Value, GameTexts.ProductName((ProductType)stationEvent.Subject)));
                        break;
                    case StationEventType.SuspiciousCustomer:
                        HudModel.Notify(Loc.T("msg.suspicious"));
                        break;
                    case StationEventType.ShoplifterCaught:
                        HudModel.Notify(Loc.T("msg.shoplifterCaught"));
                        break;
                    case StationEventType.GoodsStolen:
                        HudModel.Notify(Loc.F("msg.goodsStolen", stationEvent.Value));
                        break;
                    case StationEventType.Robbery:
                        HudModel.Notify(Loc.F("msg.robbery", stationEvent.Value));
                        break;
                    case StationEventType.RobberyPrevented:
                        HudModel.Notify(Loc.T("msg.robberyPrevented"));
                        break;
                    case StationEventType.InspectionExpiredGoods:
                        HudModel.Notify(Loc.F("msg.inspectionExpired", stationEvent.Value));
                        break;
                    case StationEventType.DinerOutOfIngredients:
                        HudModel.Notify(Loc.T("msg.dinerEmpty"));
                        break;
                    case StationEventType.FoodWasted:
                        HudModel.Notify(Loc.F("msg.foodWasted", stationEvent.Value, Loc.T($"dish.{(DinerDish)stationEvent.Subject}")));
                        break;
                    case StationEventType.DishCooked:
                        HudModel.Notify(Loc.F("msg.dishCooked", Loc.T($"dish.{(DinerDish)stationEvent.Subject}")));
                        break;
                    case StationEventType.SeasonChanged:
                        HudModel.Notify(Loc.T($"season.{(SeasonKind)(int)stationEvent.Value}.start"));
                        break;
                    case StationEventType.WeatherChanged:
                        HudModel.Notify(Loc.T($"weather.{(WeatherKind)(int)stationEvent.Value}.start"));
                        break;
                    case StationEventType.NewPlate:
                        HudModel.Notify(Loc.F("msg.newPlate", Loc.T($"plate.{stationEvent.Subject}"), stationEvent.Value, PlateMath.Count));
                        break;
                    case StationEventType.CampaignChapter:
                        HudModel.Notify(Loc.F("msg.chapter", stationEvent.Value));
                        break;
                    case StationEventType.StarGained:
                        HudModel.Notify(Loc.F("msg.starGained", GameTexts.Stars((int)stationEvent.Value)));
                        break;
                    case StationEventType.StarLost:
                        HudModel.Notify(Loc.F("msg.starLost", GameTexts.Stars((int)stationEvent.Value)));
                        break;
                    case StationEventType.RoadEventStarted:
                        HudModel.Notify(Loc.T($"road.{(RoadEventKind)(int)stationEvent.Value}.start"));
                        break;
                    case StationEventType.RoadEventEnded:
                        HudModel.Notify(Loc.T($"road.{(RoadEventKind)(int)stationEvent.Value}.end"));
                        break;
                    case StationEventType.HostedEventStarted:
                        HudModel.Notify(Loc.F("msg.eventStarted", Loc.T($"hosted.{(HostedEventKind)(int)stationEvent.Value}")));
                        break;
                    case StationEventType.HostedEventEnded:
                        HudModel.Notify(Loc.F("msg.eventEnded", stationEvent.Value, stationEvent.Subject));
                        break;
                    case StationEventType.SkillLearned:
                        HudModel.Notify(Loc.F("msg.skillLearned", Loc.T($"skill.{(OwnerSkill)(int)stationEvent.Value}")));
                        break;
                    case StationEventType.PropPlaced:
                        HudModel.Notify(Loc.F("msg.propPlaced", GameTexts.PropName((PropType)(int)stationEvent.Value)));
                        break;
                    case StationEventType.CompetitorBoughtOut:
                        HudModel.Notify(Loc.T("msg.competitorBoughtOut"));
                        break;
                }
            }

            if (weeklyBills > 0f)
                HudModel.Notify(Loc.F("msg.weeklyBills", weeklyBills));

            events.Clear();
        }
    }
}

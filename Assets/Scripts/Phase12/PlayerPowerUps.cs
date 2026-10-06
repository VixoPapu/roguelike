using System.Collections.Generic;
using UnityEngine;

namespace ProjectLike.Phase12
{
    public class PlayerPowerUps : MonoBehaviour
    {
        public List<PowerUpData> acquired = new List<PowerUpData>();
        public int Version { get; private set; }
        public bool Has(string effectId) => acquired.Exists(item => item && item.effectId == effectId);
        public int Stacks(string effectId) => acquired.FindAll(item => item && item.effectId == effectId).Count;
        public bool CanAcquire(PowerUpData powerUp) => powerUp && (powerUp.IsStackable ? Stacks(powerUp.effectId) < Mathf.Max(1, powerUp.maximumStacks) : !Has(powerUp.effectId));
        public bool Add(PowerUpData powerUp)
        {
            if (!CanAcquire(powerUp)) return false;
            acquired.Add(powerUp);
            Version++;
            var stats = GetComponent<ProjectLike.Phase1.PlayerStats>();
            if (stats) ProjectLike.Phase8.LootPickup.ApplyModifiers(stats, powerUp.modifiers, 1f);
            var runtime = GetComponent<PowerUpRuntime>();
            if (!runtime) runtime = gameObject.AddComponent<PowerUpRuntime>();
            runtime.OnAcquired(powerUp);
            return true;
        }
        public void ResetForNewRun()
        {
            acquired.Clear(); Version++;
            var runtime = GetComponent<PowerUpRuntime>(); if (runtime) runtime.ResetForNewRun();
            var timeStop = GetComponent<TimeStopAbility>(); if (timeStop) Destroy(timeStop);
        }
    }
}

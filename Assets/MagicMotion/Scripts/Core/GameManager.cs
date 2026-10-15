using System;
using UnityEngine;

namespace MagicMotion.Core
{
    [DisallowMultipleComponent]
    public sealed class GameManager : MonoBehaviour
    {
        public static GameManager Instance { get; private set; }

        public EventBus Events { get; private set; }
        public GameState State { get; private set; } = GameState.Boot;
        public bool IsReady { get; private set; }

        private void Awake()
        {
            if (Instance != null && Instance != this)
            {
                Destroy(gameObject);
                return;
            }

            Instance = this;
            DontDestroyOnLoad(gameObject);
            Events = new EventBus();
        }

        public void MarkReady()
        {
            IsReady = true;
        }

        public void ChangeState(GameState next)
        {
            if (!Enum.IsDefined(typeof(GameState), next) || next == State)
            {
                return;
            }

            var previous = State;
            State = next;
            Events.Publish(new GameStateChanged(previous, next));
        }

        private void OnDestroy()
        {
            if (Instance == this)
            {
                Events?.Clear();
                Instance = null;
            }
        }
    }
}


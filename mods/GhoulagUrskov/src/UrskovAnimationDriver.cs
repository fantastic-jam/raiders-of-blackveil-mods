using RR.Game.Character;
using UnityEngine;

namespace GhoulagUrskov {
    internal class UrskovAnimationDriver : MonoBehaviour {
        private Animator _champAnimator;
        private Animator[] _urskovAnimators;
        private static readonly int IsMoving = Animator.StringToHash("isMoving");

        internal static void Attach(GameObject instance, Animator champAnimator) {
            var driver = instance.AddComponent<UrskovAnimationDriver>();
            driver._champAnimator = champAnimator;

            foreach (var a in instance.GetComponentsInChildren<Animator>()) {
                GhoulagUrskovMod.PublicLogger.LogInfo($"[GhoulagUrskov] Found animator on '{a.gameObject.name}' controller={a.runtimeAnimatorController?.name ?? "none"} avatar={a.avatar?.name ?? "none"}");
            }

            driver._urskovAnimators = instance.GetComponentsInChildren<Animator>();
            GhoulagUrskovMod.PublicLogger.LogInfo($"[GhoulagUrskov] Driver: champAnimator={champAnimator?.name ?? "NULL"} urskovAnimators={driver._urskovAnimators.Length}");
        }

        private int _logTick;
        private int _notMovingFrames;
        private const int NotMovingThreshold = 5;

        private void Update() {
            if (_champAnimator == null || _urskovAnimators == null) {
                return;
            }

            var moving = _champAnimator.GetBool(IsMoving);
            if (!moving) {
                _notMovingFrames++;
                if (_notMovingFrames < NotMovingThreshold) {
                    moving = true;
                }
            } else {
                _notMovingFrames = 0;
            }
            foreach (var a in _urskovAnimators) {
                a.SetBool(IsMoving, moving);
            }

            if (_logTick++ % 120 == 0) {
                GhoulagUrskovMod.PublicLogger.LogInfo($"[GhoulagUrskov] isMoving={moving}");
            }
        }
    }
}

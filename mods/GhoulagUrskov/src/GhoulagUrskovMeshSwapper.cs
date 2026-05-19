using RR.Game.Character;
using UnityEngine;

namespace GhoulagUrskov {
    internal static class GhoulagUrskovMeshSwapper {
        internal static void Swap(NetworkChampionRhino champion) {
            if (!GhoulagUrskovAssets.EnsureLoaded()) {
                GhoulagUrskovMod.PublicLogger.LogError("[GhoulagUrskov] Assets failed to load — skipping swap.");
                return;
            }

            // Null out Ironhorn's meshes to hide it permanently.
            var ironhornBounds = new Bounds(champion.transform.position, Vector3.zero);
            foreach (var smr in champion.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive: false)) {
                if (smr.sharedMesh != null) {
                    ironhornBounds.Encapsulate(smr.bounds);
                }

                smr.sharedMesh = null;
            }
            var ironhornHeight = ironhornBounds.size.y;
            GhoulagUrskovMod.PublicLogger.LogInfo($"[GhoulagUrskov] ironhorn height={ironhornHeight:F4}");

            // Resolve URP shader from game runtime before instantiating.
            var urpShader = Shader.Find("Universal Render Pipeline/Lit");
            if (urpShader == null) {
                GhoulagUrskovMod.PublicLogger.LogWarning("[GhoulagUrskov] URP/Lit shader not found — materials may appear pink.");
            } else {
                GhoulagUrskovMod.PublicLogger.LogInfo("[GhoulagUrskov] URP/Lit shader found.");
            }

            // Capture champion Animator before instantiating Urskov (GetComponentInChildren would also find Urskov's after).
            var champAnimator = champion.GetComponentInChildren<Animator>();
            if (champAnimator == null) {
                GhoulagUrskovMod.PublicLogger.LogWarning("[GhoulagUrskov] Champion Animator not found — isMoving won't be mirrored.");
            }

            // Parent to CharMesh so Urskov inherits Ironhorn's visual rotation.
            var parent = champion.CharMesh != null ? champion.CharMesh.transform : champion.transform;
            var instance = Object.Instantiate(GhoulagUrskovAssets.Prefab, parent);
            instance.transform.localPosition = Vector3.zero;
            instance.transform.localRotation = Quaternion.identity;
            instance.transform.localScale = Vector3.one;

            // Replace bundled materials with runtime URP shader, preserving the texture.
            if (urpShader != null) {
                foreach (var r in instance.GetComponentsInChildren<Renderer>(includeInactive: true)) {
                    var oldMat = r.sharedMaterial;
                    var newMat = new Material(urpShader);
                    if (oldMat != null) {
                        newMat.SetTexture("_BaseMap", oldMat.mainTexture);
                        newMat.color = oldMat.color;
                    }
                    r.sharedMaterial = newMat;
                }
            }

            // Auto-scale to match Ironhorn's height.
            // var urskovBounds = new Bounds(instance.transform.position, Vector3.zero);
            // foreach (var r in instance.GetComponentsInChildren<Renderer>(includeInactive: false))
            //     urskovBounds.Encapsulate(r.bounds);
            // var urskovHeight = urskovBounds.size.y;
            // if (urskovHeight > 0f && ironhornHeight > 0f) {
            //     var scale = ironhornHeight / urskovHeight;
            //     instance.transform.localScale = Vector3.one * scale;
            //     GhoulagUrskovMod.PublicLogger.LogInfo($"[GhoulagUrskov] scale={scale:F6}");
            // }
            instance.transform.localScale = Vector3.one * 100f;

            var animator = instance.GetComponentInChildren<Animator>();
            if (animator != null) {
                animator.applyRootMotion = false;
                UrskovAnimationDriver.Attach(instance, champAnimator);
                GhoulagUrskovMod.PublicLogger.LogInfo("[GhoulagUrskov] Animation driver attached.");
            } else {
                GhoulagUrskovMod.PublicLogger.LogWarning("[GhoulagUrskov] No Animator found on instance.");
            }

            GhoulagUrskovMod.PublicLogger.LogInfo("[GhoulagUrskov] Swap done.");
        }
    }
}

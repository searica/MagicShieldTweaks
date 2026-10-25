using HarmonyLib;
using System.Collections.Generic;
using Jotunn.Managers;
using UnityEngine;
using Logging;
using MagicShieldTweaks.Extensions;
using System.Reflection;

namespace MagicShieldTweaks
{
    [HarmonyPatch]
    internal class ShieldPatches
    {
        private readonly static Dictionary<SE_Shield, MeshRenderer> s_shieldSpheres = new Dictionary<SE_Shield, MeshRenderer>();
        private readonly static MaterialPropertyBlock s_matBlock = new MaterialPropertyBlock();

        private static readonly FieldInfo[] AttackFields = typeof(Attack).GetFields();

        //private const string PatchedSecondaryAttackName = "staff_trollsummon";
        //private const string PatchedSecondaryAttackName = "staff_fireball";


        private const string ShieldStaffPrefabName = "StaffShield";
        private const string ReactStaffPrefabName = "StaffFrostOrbs";

        private static readonly HashSet<string> StaffPrefabNames = [ShieldStaffPrefabName, ReactStaffPrefabName];
        private static readonly Dictionary<string, string> StaffItemDataNamesToAnimations = new() {
            { "$item_staffshield", "staff_trollsummon"},
            //{ "$item_staff_frostorbs", "staff_fireball"}, // I think fireball doesn't work because it needs a projectile velocity
            
            //{ "$item_staff_frostorbs", "attack_protect"},
            //{ "$item_staff_frostorbs", "attack_cast"},
            { "$item_staff_frostorbs", "staff_trollsummon"},
        };

        private static readonly HashSet<string> PatchedSecondaryAttackNames = new( StaffItemDataNamesToAnimations.Values );

        private static ItemDrop.ItemData CurrentWeapon;
        private static Attack CurrentAttack;


        [HarmonyPrefix]
        [HarmonyPriority(Priority.Last)]
        [HarmonyPatch(typeof(SE_Shield), nameof(SE_Shield.OnDamaged))]
        private static bool SE_Shield_OnDamaged_PreventEffectSpam(ref HitData hit)
        {
            if (MagicShieldTweaks.Instance.PreventZeroDamageShieldSpam.Value && hit.GetTotalDamage() == 0)
            {
                return false;
            }
                    
            if (MagicShieldTweaks.Instance.PreventLookVectorConsoleSpam.Value && hit.m_dir.sqrMagnitude < 0.1f)
            {
                hit.m_dir = Vector3.down;
            }
            return true;
        }

        [HarmonyPostfix]
        [HarmonyPriority(Priority.First)]
        [HarmonyPatch(typeof(SE_Shield), nameof(SE_Shield.OnDamaged))]
        private static void UpdateShieldColorOnDamage(SE_Shield __instance, ref HitData hit)
        {
            UpdateShieldColor(__instance);
        }
    

        private static void UpdateShieldColor(SE_Shield shield)
        {
            if (!MagicShieldTweaks.Instance.ChangeShieldColorByHealth.Value || shield.m_totalAbsorbDamage <= 0f ||
                 shield.m_startEffectInstances == null || shield.m_startEffectInstances.Length == 0 || !shield.m_startEffectInstances[0])
            {
                return;
            }

            if (!s_shieldSpheres.TryGetValue(shield, out MeshRenderer sphere) || !sphere)
            {
                Transform sphereTransform = shield.m_startEffectInstances[0].transform.Find("Sphere");
                if (!sphereTransform || !sphereTransform.TryGetComponent(out sphere) || !sphere)
                {
                    return;
                }
                s_shieldSpheres[shield] = sphere;
            }

            if (!sphere.sharedMaterial)
            {
                return;
            }
                
            sphere.GetPropertyBlock(s_matBlock);
            s_matBlock.SetColor(
                ShaderProps._Color, 
                Color.Lerp(
                    sphere.sharedMaterial.color, 
                    MagicShieldTweaks.Instance.ShieldTargetColorZeroHealth.Value, 
                    shield.m_damage / shield.m_totalAbsorbDamage
                )
            );
            sphere.SetPropertyBlock(s_matBlock);
        }

        [HarmonyPostfix]
        [HarmonyPatch(typeof(SE_Shield), nameof(SE_Shield.IsDone))]
        private static void SE_Shield_IsDone_ClearCachedSphere(SE_Shield __instance, bool __result)
        {
            if (__result)
            {
                s_shieldSpheres.Remove(__instance);
            }
        }


        [HarmonyPostfix]
        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.OnDestroy))]
        private static void ZoneSystem_OnDestroy_ClearSpheresCache()
        {
            s_shieldSpheres.Clear();
        }

        public static void PatchStaffShield(ItemDrop.ItemData item)
        {
            if (item == null || !StaffItemDataNamesToAnimations.TryGetValue(item.m_shared.m_name, out string attackName))
            {
                return;
            }

            Attack attack = item.m_shared.m_secondaryAttack;

            if (attack == null || attack.m_attackAnimation == attackName)
            {
                return;
            }

            AttackFields.Do(f => f.SetValue(attack, f.GetValue(item.m_shared.m_attack)));
            attack.m_attackEitr = 0.0f;
            attack.m_attackHealthPercentage = 0.0f;
            attack.m_attackAdrenaline = 0.0f;
            attack.m_attackUseAdrenaline = 0.0f;
            attack.m_attackHealthLowBlockUse = false;
            attack.m_attackAnimation = attackName;
            attack.m_startEffect = new EffectList();
        }
        
  
        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        [HarmonyPatch(typeof(ZoneSystem), nameof(ZoneSystem.Start))]
        private static void AddStaffShieldSecondaryAttackOnZoneSystemStart()
        {
            foreach (string prefabName in StaffPrefabNames)
            {
                GameObject staff = PrefabManager.Instance.GetPrefab(prefabName);
                if (staff && staff.TryGetComponent(out ItemDrop itemDrop))
                {
                    PatchStaffShield(itemDrop.m_itemData);
                }
            }
        }


        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        [HarmonyPatch(typeof(Player), nameof(Player.Load))]
        private static void AddStaffShieldSecondaryAttackToPlayerItems(Player __instance)
        {
            if (!__instance || !Player.m_localPlayerExists || __instance != Player.m_localPlayer)
            {
                return;
            }
            __instance.GetInventory().m_inventory.Do(PatchStaffShield);
        }


        [HarmonyPostfix]
        [HarmonyPriority(Priority.Last)]
        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.EquipItem))]
        private static void AddStaffShieldSecondaryAttackOnEquipItem(Humanoid __instance, ItemDrop.ItemData item, bool __result)
        {
            if (!__result || !__instance || !__instance.IsPlayer() || __instance != Player.m_localPlayer)
            {
                return;
            }
            PatchStaffShield(item);
        }


        [HarmonyPatch(typeof(Humanoid), nameof(Humanoid.StartAttack))]
        public static class Humanoid_StartAttack_Staff
        {
            private static bool Prefix(Humanoid __instance, bool secondaryAttack)
            {
                if (secondaryAttack && TryGetPlayerWeapon(__instance, out ItemDrop.ItemData weapon) && StaffItemDataNamesToAnimations.ContainsKey(weapon.m_shared.m_name))
                {
                    // prevent starting the attack unless there is no status effect applied by the weapon, or the player already has the status effect it applies active
                    return weapon.m_shared.m_attackStatusEffect == null || Player.m_localPlayer.GetSEMan().HaveStatusEffect(weapon.m_shared.m_attackStatusEffect.NameHash());
                }

                return true;
            }

            private static void Postfix(Humanoid __instance, bool __result)
            {
                if (__result && TryGetPlayerWeapon(__instance, out ItemDrop.ItemData weapon) && 
                    __instance.m_currentAttackIsSecondary && StaffItemDataNamesToAnimations.ContainsKey(weapon.m_shared.m_name))
                {
                    CurrentWeapon = weapon;
                    CurrentAttack = __instance.m_currentAttack;
                }
            }
        }

        /// <summary>
        ///     Postfix patch runs on all attacks but we only care about getting the weapon from the local player
        /// </summary>
        /// <param name="attack"></param>
        /// <returns></returns>
        private static bool TryGetPlayerWeapon(Humanoid humanoid, out ItemDrop.ItemData weapon)
        {
            if (!humanoid || !humanoid.IsPlayer() || humanoid != Player.m_localPlayer)
            {
                weapon = null;
                return false;
            }

            weapon = humanoid.GetCurrentWeapon();
            return weapon != null;
        }


        [HarmonyPatch(typeof(Attack), nameof(Attack.FireProjectileBurst))]
        public static class Attack_FireProjectileBurst_Staff
        {
            private static float StatusEffectChance = 1.0f;
            //private static ItemDrop.ItemData CurrentWeapon;

            private static void Prefix(Attack __instance, ref bool __state)
            {
                if (__instance == CurrentAttack &&  __instance.m_weapon == CurrentWeapon)
                {
                    __state = true;
                    StatusEffectChance = CurrentWeapon.m_shared.m_attackStatusEffectChance;
                    CurrentWeapon.m_shared.m_attackStatusEffectChance = 0.0f;
                }
                else
                {
                    __state = false;
                }                    
            }

            private static void Postfix(bool __state)
            {
                if (!__state)
                {
                    return;
                }

                CurrentWeapon.m_shared.m_attackStatusEffectChance = StatusEffectChance;
                if (CurrentWeapon.m_shared.m_attackStatusEffect is SE_Shield shield)
                {
                    shield.m_breakEffects.Create(
                        Player.m_localPlayer.GetCenterPoint(), Player.m_localPlayer.transform.rotation, 
                        Player.m_localPlayer.transform, Player.m_localPlayer.GetRadius() * 2f
                    );
                    Player.m_localPlayer.GetSEMan().RemoveStatusEffect(shield);
                }
                else if (CurrentWeapon.m_shared.m_attackStatusEffect is SE_React react)
                {
                    react.m_stopEffects.Create(
                        Player.m_localPlayer.GetCenterPoint(), Player.m_localPlayer.transform.rotation,
                        Player.m_localPlayer.transform, Player.m_localPlayer.GetRadius() * 2f
                    );
                    Player.m_localPlayer.GetSEMan().RemoveStatusEffect(react);
                }
                
            }
        }
    }
}

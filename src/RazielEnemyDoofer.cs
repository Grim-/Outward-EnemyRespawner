using BepInEx;
using BepInEx.Configuration;
using BepInEx.Logging;
using HarmonyLib;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

namespace OutwardModTemplate
{
    /* ~~~ Initial Setup ~~~
     * 
     * 1. Double click on "Properties" in the Solution Explorer panel to the left
     * 2. Click on the "Application" tab
     * 3. Change your "Assembly Name" and "Default Namespace" to something unique for your mod.
     * 4. Right click the namespace "OutwardModTemplate" above and rename it to what you chose for your Namespace.
     * 5. Right click "MyMod" below and rename it, generally you use what you chose for your Assembly Name.
     * 6. Read the rest of the comments in this file and make changes as needed.
     */

    [BepInPlugin(GUID, NAME, VERSION)]
    public class RazielEnemyDoofer : BaseUnityPlugin
    {
        // Choose a GUID for your project. Change "myname" and "mymod".
        public const string GUID = "myname.mymod";
        // Choose a NAME for your project, generally the same as your Assembly Name.
        public const string NAME = "MyMod";
        // Increment the VERSION when you release a new version of your mod.
        public const string VERSION = "1.0.0";
        // For accessing your BepInEx Logger from outside of this class (MyMod.Log)
        internal static ManualLogSource Log;

        // If you need settings, define them like so:
        public static ConfigEntry<bool> ResetAllEnemies;
        public static ConfigEntry<float> ResurrectDelayTime;

        private int LastSceneIndex;

        internal void Awake()
        {
            Log = this.Logger;

            ResetAllEnemies = Config.Bind(NAME, "Reset All Enemys", true, "Reset All Enemys when you enter an Area?");
            ResurrectDelayTime = Config.Bind(NAME, "Resurrection Delay Time", 6f, "A delay is required after the scene has loaded in order to properly respawn enemies, the default value is usually enough for most cases.");

            SceneManager.sceneLoaded += SceneManager_sceneLoaded;
            SceneManager.sceneUnloaded += SceneManager_sceneUnloaded;
        }



        private void SceneManager_sceneLoaded(Scene Scene, LoadSceneMode LoadMode)
        {
            if (Scene.name == "MainMenu_Empty" || Scene.name == "LowMemory_TransitionScene")
            {
                return;
            }

            Log.LogMessage($"Scene Loaded!");


            LastSceneIndex = Scene.buildIndex;

            if (ResetAllEnemies.Value)
            {
                ResetEnemies();
            }
        }

        private void SceneManager_sceneUnloaded(Scene Scene)
        {
            if (Scene.name == "MainMenu_Empty" || Scene.name == "LowMemory_TransitionScene")
            {
                return;
            }

            if (Scene.buildIndex == LastSceneIndex && LastSceneIndex != 0)
            {
                //scene has changed
            }
        }

        private void ResetEnemies()
        {
            //You need to delay some time after the scenes loaded because the objects still havent finished setting up.
            DelayDo(() =>
            {
                Log.LogMessage($"Restoring all Squads.");

                foreach (var squadCharacter in GetCharactersFromAISquads())
                {
                    if (squadCharacter.IsDead || squadCharacter.m_loadedDead)
                    {
                        ReviveCharacter(squadCharacter);
                    }

                }

                Log.LogMessage($"Restoring all Statics.");
                foreach (var staticCharacter in GetCharactersFromStaticContainer())
                {
                    if (staticCharacter.IsDead || staticCharacter.m_loadedDead)
                    {
                        ReviveCharacter(staticCharacter);
                    }

                }

            }, ResurrectDelayTime.Value);
        }

        private void ReviveCharacter(Character CharacterToRevive, bool RestoreWeaponIfDropped = true)
        {
            Log.LogMessage($"Restoring {CharacterToRevive.Name}");
            CharacterToRevive.ResetCombat();
            CharacterToRevive.ResetPosition();
            CharacterToRevive.GetComponent<CharacterAI>().SwitchAiState(0);
            CharacterToRevive.SendResurrect(true, null, true);
            CharacterToRevive.SendDeathRPC(false, Vector3.zero, false);

            if (RestoreWeaponIfDropped)
            {
                if (CharacterToRevive.CurrentWeapon == null)
                {
                    Weapon StartingWeapon = FindStartingWeapon(CharacterToRevive);

                    if (StartingWeapon != null)
                    {
                        CharacterToRevive.Inventory.EquipInstantiate(StartingWeapon);
                    }
                   
                }
            }
        }


        private Weapon FindStartingWeapon(Character Character)
        {
            StartingEquipment startingEquipmentComp = Character.GetComponent<StartingEquipment>();

            if (startingEquipmentComp != null)
            {
                foreach (var item in startingEquipmentComp.m_startingEquipment)
                {
                    if (item is Weapon)
                    {
                        return item as Weapon;
                    }
                }
            }

            return null;
        }

        /// <summary>
        /// Attempts to get all the characters from AISquads in the current scene, including already dead which are parented elsewhere.
        /// </summary>
        /// <returns></returns>
        private List<Character> GetCharactersFromAISquads()
        {
            List<Character> characters = new List<Character>();

            foreach (var item in AISquadManager.Instance.m_allSquads.Values)
            {
                foreach (var character in item.Members)
                {
                    if (!characters.Contains(character.Character))
                    {
                        characters.Add(character.Character);
                    }
                }
            }

            foreach (var item in AISquadManager.Instance.transform.GetComponentsInChildren<Character>())
            {
                if (!characters.Contains(item))
                {
                    characters.Add(item);
                }

            }

            return characters;
        }
        /// <summary>
        /// Attempts to get all the characters from Static Enemies Container
        /// </summary>
        /// <returns></returns>
        private List<Character> GetCharactersFromStaticContainer()
        {
            List<Character> characters = new List<Character>();

            Transform StaticEnemyContainer = AISceneManager.Instance.transform;

            if (StaticEnemyContainer)
            {
                foreach (var character in StaticEnemyContainer.GetComponentsInChildren<CharacterAI>())
                {
                    characters.Add(character.Character);
                }
            }
            else Logger.LogMessage("Static Enemy Container not found");

            return characters;
        }



        public void DelayDo(Action OnAfterDelay, float DelayTime)
        {
            StartCoroutine(DoAfterDelay(OnAfterDelay, DelayTime));
        }

        public IEnumerator DoAfterDelay(Action OnAfterDelay, float DelayTime)
        {
            yield return new WaitForSeconds(DelayTime);
            OnAfterDelay.Invoke();
            yield break;
        }
    }
}

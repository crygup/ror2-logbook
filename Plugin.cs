using System;
using System.Linq;
using System.Reflection;
using BepInEx;
using BepInEx.Logging;
using HarmonyLib;
using RoR2;
using RoR2.UI;
using RoR2.UI.LogBook;
using TMPro;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace LogbookMarkRead
{
    [BepInPlugin(Id, "Logbook Mark Read", "1.0.0")]
    public sealed class Plugin : BaseUnityPlugin
    {
        public const string Id = "local.liz.logbookmarkread";
        internal static ManualLogSource Log;
        private Harmony harmony;

        private void Awake()
        {
            Log = Logger;
            harmony = new Harmony(Id);
            harmony.Patch(AccessTools.Method(typeof(LogBookController), "Start"),
                postfix: new HarmonyMethod(typeof(Plugin), nameof(AddButton)));
        }

        private static void AddButton(LogBookController __instance)
        {
            if (!__instance.GetComponent<PageButton>())
                __instance.gameObject.AddComponent<PageButton>();
        }

        private void OnDestroy() => harmony?.UnpatchSelf();
    }

    public sealed class PageButton : MonoBehaviour
    {
        private static readonly FieldInfo CurrentPage =
            AccessTools.Field(typeof(LogBookController), "currentEntriesPageObject");
        private static readonly FieldInfo CurrentCategory =
            AccessTools.Field(typeof(LogBookController), "currentCategoryIndex");
        private LogBookController controller;
        private EntityStateMachine stateMachine;
        private MPButton button;
        private RectTransform rect;
        private RectTransform previous;

        private void Start()
        {
            try
            {
                controller = GetComponent<LogBookController>();
                stateMachine = GetComponent<EntityStateMachine>();
                previous = (RectTransform)controller.previousPageButton.transform;
                // Use the game's text button prefab, including its art, sounds and hover state.
                var obj = Instantiate(controller.categoryButtonPrefab, previous.parent, false);
                obj.name = "MarkAllAsRead";
                button = obj.GetComponent<MPButton>();
                button.onClick = new Button.ButtonClickedEvent();
                button.onSelect = new UnityEvent();
                button.onDeselect = new UnityEvent();
                button.onDebugSelect = new UnityEvent();
                button.onFindSelectableLeft = new UnityEvent();
                button.onFindSelectableRight = new UnityEvent();
                button.defaultFallbackButton = false;
                button.requiredTopLayer = controller.GetComponent<UILayerKey>();
                button.disableGamepadClick = false;
                button.disablePointerClick = false;
                button.onClick.AddListener(MarkCurrentCategory);
                button.navigation = new Navigation { mode = Navigation.Mode.Automatic };
                if (button is HGButton hg) hg.updateTextOnHover = false;
                foreach (var language in obj.GetComponentsInChildren<LanguageTextMeshController>(true))
                    language.token = "Mark all as read";
                var label = obj.GetComponentInChildren<TMP_Text>(true);
                label.text = "Mark all as read";
                label.alignment = TextAlignmentOptions.Center;
                label.enableAutoSizing = true;
                label.fontSizeMin = 12;
                label.fontSizeMax = 24;
                label.enableWordWrapping = false;
                var layout = obj.GetComponent<LayoutElement>() ?? obj.AddComponent<LayoutElement>();
                layout.ignoreLayout = true;
                rect = (RectTransform)obj.transform;
                rect.anchorMin = rect.anchorMax = previous.anchorMin;
                rect.pivot = new Vector2(1, 0.5f);
                obj.SetActive(true);
            }
            catch (Exception error)
            {
                Plugin.Log.LogError("Could not add the logbook button: " + error);
                if (button) Destroy(button.gameObject);
                enabled = false;
            }
        }

        private bool CanMark()
        {
            return controller && controller.isActiveAndEnabled &&
                controller.navigationPanel.activeInHierarchy &&
                stateMachine && stateMachine.state is EntityStates.Idle &&
                CurrentPage.GetValue(controller) is GameObject page && page.activeInHierarchy;
        }

        private void LateUpdate()
        {
            if (!button || !previous) return;
            button.gameObject.SetActive(controller.navigationPanel.activeInHierarchy);
            button.interactable = CanMark();
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, previous.rect.height * 3.65f);
            rect.SetSizeWithCurrentAnchors(RectTransform.Axis.Vertical, previous.rect.height);
            rect.position = previous.TransformPoint(new Vector3(previous.rect.xMin - 6, previous.rect.center.y));
        }

        private void MarkCurrentCategory()
        {
            if (!CanMark()) return;
            try
            {
                int categoryIndex = (int)CurrentCategory.GetValue(controller);
                if (categoryIndex < 0 || categoryIndex >= LogBookController.categories.Length) return;
                var profile = LocalUserManager.readOnlyLocalUsersList.FirstOrDefault(v => v != null)?.userProfile;
                // The category's registered tree includes vanilla and modded entries on every page.
                int count = PageRead.MarkCategory(LogBookController.categories[categoryIndex].viewableNode, profile);
                if (count > 0) profile.SaveIfNecessary();
                Plugin.Log.LogInfo($"Marked {count} logbook entries as read in the selected category.");
            }
            catch (Exception error)
            {
                Plugin.Log.LogError("Could not mark the logbook category as read: " + error);
            }
        }

        private void OnDestroy()
        {
            if (button) Destroy(button.gameObject);
        }
    }
}


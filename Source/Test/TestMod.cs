#if TEST_MOD
using System;
using System.Collections.Generic;
using System.Linq;
using System.Xml.Serialization;
using HugsLib.Settings;
using HugsLib.Source.Settings;
using HugsLib.Utils;
using UnityEngine;
using UnityEngine.SceneManagement;
using Verse;

namespace HugsLib.Test {
	/// <summary>
	/// This mod is for testing the various facilities of the library
	/// </summary>
	[EarlyInit]
	public class TestMod : ModBase {
		public static TestMod Instance { get; private set; }

		internal new ModLogger Logger {
			get { return base.Logger; }
		}

		public TestMod() {
			Instance = this;
		}

		public override string ModIdentifier {
			get { return "TestMod"; }
		}

		protected override bool HarmonyAutoPatch {
			get { return false; }
		}

		public override void EarlyInitialize() {
			Logger.Message("Early-initialized");
		}

		public override void StaticInitialize() {
			Logger.Message("Static-initialized");
			TestDoLaterScheduler();
		}

		private void TestDoLaterScheduler() {
			var doLater = HugsLibController.Instance.DoLater;
			doLater.DoNextTick(() => Logger.Message("DoLater: tick"));
			doLater.DoNextUpdate(() => Logger.Message("DoLater: update"));
			doLater.DoNextOnGUI(() => Logger.Message("DoLater: OnGUI " + Event.current.type));
			doLater.DoNextMapLoaded(map => Logger.Message("DoLater: MapLoaded " + map));

			/*Logger.Message("Testing recurring doLater...");
			var numCalls = 0;
			Action everyFrame = null;
			everyFrame = () => {
				if (numCalls < 60) {
					HugsLibController.Instance.DoLater.DoNextUpdate(everyFrame);
					numCalls++;
					Logger.Message("Recurring doLater progress");
				} else {
					Logger.Message("Recurring doLater success");
				}
			};
			everyFrame();*/

			Action<Map> everyMapLoaded = null;
			everyMapLoaded = map => {
				HugsLibController.Instance.DoLater.DoNextMapLoaded(everyMapLoaded);
				Logger.Message("Recurring doLater: map loaded: " + map);
			};
			HugsLibController.Instance.DoLater.DoNextMapLoaded(everyMapLoaded);
		}

		#pragma warning disable 649 // unassigned field
		[TweakValue("HugsLibTesting")]
		private static bool TraceTick;
		public override void Tick(int currentTick) {
			if(TraceTick) Logger.Message("Tick:"+currentTick);
		}

		[TweakValue("HugsLibTesting")]
		private static bool TraceUpdate;
		public override void Update() {
			if(TraceUpdate) Logger.Message("Update: "+Time.frameCount);
		}

		[TweakValue("HugsLibTesting")]
		private static bool TraceFixedUpdate;
		public override void FixedUpdate() {
			if(TraceFixedUpdate) Logger.Message("FixedUpdate");
		}

		[TweakValue("HugsLibTesting")]
		private static bool TraceOnGUI;
		public override void OnGUI() {
			if(TraceOnGUI) Logger.Message("OnGUI: "+Event.current.type);
		}

		public override void WorldLoaded() {
			Logger.Message("WorldLoaded");
		}

		public override void MapComponentsInitializing(Map map) {
			Logger.Message("MapComponentsInitializing on map:" + map.Index);
		}

		public override void MapGenerated(Map map) {
			Logger.Message("MapGenerated:" + map);
		}

		public override void MapLoaded(Map map) {
			Logger.Message("MapLoaded:" + map);
			try {
				map.mapDrawer.MapMeshDirty(new IntVec3(0, 0, 0), MapMeshFlag.Buildings);
			} catch (Exception e) {
				Logger.Error("MapLoaded fired before map mesh regeneration " + e);
			}
		}

		public override void MapDiscarded(Map map) {
			Logger.Message("MapDiscarded:" + map);
		}

		public override void SceneLoaded(Scene scene) {
			Logger.Message("SceneLoaded:" + scene.name);
		}

		private bool settingsChangedCalled;
		public override void SettingsChanged() {
			Logger.Message("SettingsChanged");
			settingsChangedCalled = true;
		}

		private enum HandleEnum {
			DefaultValue,
			ValueOne,
			ValueTwo
		}

		public override void DefsLoaded() {
			Logger.Message("DefsLoaded");
			Settings.GetHandle("str", "String value", "", "value");
			var spinner = Settings.GetHandle("intSpinner", "Spinner", "desc", 5, Validators.IntRangeValidator(0, 30));
			spinner.SpinnerIncrement = 2;
			spinner.CanBeReset = false;
			Settings.GetHandle("enumThing", "Enum setting", "", HandleEnum.DefaultValue, null, "test_enumSetting_");
			Settings.GetHandle("toggle", "Toggle setting extra long title that would not fit into one line", "Toggle setting", false);
			var custom = Settings.GetHandle("custom", "custom setting", "custom setting desc", false);
			custom.CustomDrawerHeight = 30f;
			custom.CustomDrawer = rect => {
				if (Widgets.ButtonText(new Rect(rect.x, rect.y, rect.width, custom.CustomDrawerHeight), "I Iz Button")) {
					custom.CustomDrawerHeight = custom.CustomDrawerHeight > 30 ? 30f : 400f;
				}
				return false;
			};
			var fullWidth = Settings.GetHandle("fullWidth", null, null, false);
			fullWidth.CustomDrawerHeight = 30f;
			fullWidth.CustomDrawerFullWidth = rect => {
				float SineColor(float offset) => .6f + .4f * Mathf.Sin(offset + Time.unscaledTime); 
				GUI.color = new Color(SineColor(.5f), SineColor(1f), SineColor(1.5f));
				Widgets.Label(rect, "Full width goodness");
				GUI.color = fullWidth.Value ? new Color(.5f, 1f, .5f) : new Color(1f, .5f, .5f);
				bool changed = false;
				if (Widgets.ButtonText(rect.RightHalf(), "Clicky")) {
					fullWidth.Value = !fullWidth.Value;
					changed = true;
				}
				GUI.color = Color.white;
				return changed;
			};
			
			TestSettingsHasUnsavedChanges();
			TestCustomTypeSetting();
			TestGiveShortHash();
			//TestConditionalVisibilitySettings();	
		}

		private void TestGiveShortHash() {
			var def = new Def{defName = "randomDefForTesting"};
			InjectedDefHasher.GiveShortHashToDef(def, typeof(Def));
			if (def.shortHash == 0) {
				Logger.Error("GiveShortHasToDef has failed");
			} else {
				Logger.Message("Given short hash: "+def.shortHash);
			}
		}


		private void TestConditionalVisibilitySettings() {
			for (int i = 0; i < 50; i++) {
				var toggle = Settings.GetHandle("toggle" + i, "toggle", null, false);
				var index = i;
				toggle.VisibilityPredicate = () => Input.mousePosition.x/22 < index;
			}
		}

		private void TestSettingsHasUnsavedChanges() {
			void Assert(bool condition, string expectedConditionMessage) {
				if(!condition) HugsLibController.Logger.Error($"Expected {nameof(TestSettingsHasUnsavedChanges)} condition: {expectedConditionMessage}");
			}
			var controllerSaved = false;
			TestModSettingsChangedDetector.SettingsChangedCalled = false;
			void OnControllerSaved() {
				controllerSaved = true;
			}
			HugsLibController.SettingsManager.AfterModSettingsSaved += OnControllerSaved;
			settingsChangedCalled = false;
			var handle = Settings.GetHandle<int>("changeTestHandle", null, null);
			handle.NeverVisible = true;
			
			if (HugsLibController.SettingsManager.HasUnsavedChanges) {
				HugsLibController.Logger.Warning("Already modified handles: "+HugsLibController.SettingsManager.ModSettingsPacks
					.SelectMany(p => p.Handles).Where(h => h.HasUnsavedChanges).Select(h => h.Name).ListElements());
			}
			Assert(HugsLibController.SettingsManager.HasUnsavedChanges == false, "controller unsaved false");
			
			Settings.SaveChanges();

			Assert(controllerSaved == false, "controller not saving without changes");
			Assert(settingsChangedCalled == false, "SettingsChanged not called before");
			Assert(handle.HasUnsavedChanges == false, "handle unsaved false");
			Assert(Settings.HasUnsavedChanges == false, "pack unsaved false");
			
			handle.Value += 1;
			
			Assert(handle.HasUnsavedChanges, "handle unsaved true");
			Assert(Settings.HasUnsavedChanges, "pack unsaved true");
			Assert(HugsLibController.SettingsManager.HasUnsavedChanges, "controller unsaved true");

			Settings.SaveChanges();

			Assert(controllerSaved, "controller saved changes");
			Assert(settingsChangedCalled, "SettingsChanged called after");
			Assert(handle.HasUnsavedChanges == false, "handle unsaved after false");
			Assert(Settings.HasUnsavedChanges == false, "pack unsaved after false");
			Assert(TestModSettingsChangedDetector.SettingsChangedCalled == false, "foreign mod not notified");
			Assert(HugsLibController.SettingsManager.HasUnsavedChanges == false, "controller unsaved after false");

			settingsChangedCalled = false;
			TestModSettingsChangedDetector.SettingsChangedCalled = false;
			TestModSettingsChangedDetector.Handle.Value += 1;
			Settings.SaveChanges();
			Assert(settingsChangedCalled == false, "our mod not notified");
			Assert(TestModSettingsChangedDetector.SettingsChangedCalled, "foreign mod notified");

			settingsChangedCalled = false;
			Assert(handle.Value != handle.DefaultValue, "has non-default value");
			Assert(handle.HasUnsavedChanges == false, "saved before default value");
			handle.Value = handle.DefaultValue;
			Settings.SaveChanges();
			Assert(handle.HasUnsavedChanges == false, "default value handle after has no unsaved changes");
			Assert(settingsChangedCalled, "default value propagated save");

			HugsLibController.SettingsManager.AfterModSettingsSaved -= OnControllerSaved;
		}

		private void TestCustomTypeSetting() {
			var custom = Settings.GetHandle<CustomHandleType>("customType", null, null);
			custom.NeverVisible = true;
			if (custom.Value == null) custom.Value = new CustomHandleType { Nums = new List<int>() };
			custom.Value.Nums.Add(Rand.Range(1, 100));
			if (custom.Value.Nums.Count > 10) {
				custom.Value.Nums.RemoveAt(0);
			}
			custom.Value.Prop++;
			custom.ForceSaveChanges();
			Logger.Trace($"Custom setting values: Nums:{custom.Value.Nums.Join(",")} Prop:{custom.Value.Prop}");
		}

		//<customType>aasd1w423</customType>
		[Serializable]
		public class CustomHandleType : SettingHandleConvertible {
			[XmlElement] public List<int> Nums = new List<int>();

			[XmlElement]
			public int Prop { get; set; }

			public override void FromString(string settingValue) {
				SettingHandleConvertibleUtility.DeserializeValuesFromString(settingValue, this);
			}

			public override string ToString() {
				return SettingHandleConvertibleUtility.SerializeValuesToString(this);
			}
		}

	}

	/// <summary>
	/// Used to ensure only mods with actually changed settings receive the SettingsChanged callback
	/// </summary>
	[EarlyInit]
	public class TestModSettingsChangedDetector : ModBase {
		public static SettingHandle<int> Handle { get; set; }
		public static bool SettingsChangedCalled { get; set; }

		public override string ModIdentifier => "SettingsChangedDetector";
		protected override bool HarmonyAutoPatch => false;

		public override void Initialize() {
			Handle = Settings.GetHandle<int>("testHandle", null, null);
			Handle.NeverVisible = true;
		}

		public override void SettingsChanged() {
			SettingsChangedCalled = true;
		}
	}

}
#endif
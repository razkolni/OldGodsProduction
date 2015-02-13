using System.Collections.Generic;
using System.IO;
using UnityEditor;
using UnityEngine;

#if UNITY_5_0
using UnityEngine.Audio;
#endif

[CustomEditor(typeof(MasterAudio))]
// ReSharper disable once CheckNamespace
public class MasterAudioInspector : Editor {
    public const string NewBusName = "[NEW BUS]";
    public const string RenameMeBusName = "[BUS NAME]";

    public const string SpatialBlendSliderText = "For the slider below, 0 is fully 2D and 1 is fully 3D.";

    private const string NoMuteSoloAllowed = "You cannot mute or solo this Group because the bus it uses is soloed or muted. Please unmute or unsolo the bus instead.";
    private const string GroupTemplateFolder = MasterAudio.GroupTemplateFolder;

    public static readonly Color InactiveClr = new Color(.00f, .77f, .33f);
    public static readonly Color ActiveClr = new Color(.33f, .99f, .66f);

    private static MasterAudio _sounds;

    private bool _isValid = true;

    // ReSharper disable once InconsistentNaming
    public List<MasterAudioGroup> groups = new List<MasterAudioGroup>();

    private List<string> _groupTemplateNames = new List<string>();
    private List<string> _audioSourceTemplateNames = new List<string>();
    private List<string> _playlistNames = new List<string>();
    private bool _isDirty;
    private GameObject _previewer;

    // ReSharper disable once InconsistentNaming
    // ReSharper disable once FieldCanBeMadeReadOnly.Local
    private static List<MAObjectContext> allChangePersisters = new List<MAObjectContext>();
    private static bool _persistChanges;

    private readonly List<float> _reevaluatePriorityTimes = new List<float>() {
		.1f,
		.2f,
		.3f,
		.4f,
		.5f,
		.6f,
		.7f,
		.8f,
		.9f,
		1.0f
	};

    #region Change Persisting Code
    public void OnEnable() {
        EditorApplication.playmodeStateChanged = PlaymodeStateChanged;
    }

    private static void AddAllChangePersistingObjects() {
        var ma = MasterAudio.Instance;

        _persistChanges = ma.saveRuntimeChanges;

        AddPersistingChangeObject(ma.transform);

        for (var i = 0; i < ma.transform.childCount; i++) {
            var aGroup = ma.transform.GetChild(i);

            AddPersistingChangeObject(aGroup);

            for (var v = 0; v < aGroup.transform.childCount; v++) {
                var aVar = aGroup.transform.GetChild(v);

                AddPersistingChangeObject(aVar.transform);
            }
        }

        var controllers = PlaylistController.Instances;
        foreach (var aController in controllers) {
            AddPersistingChangeObject(aController.transform);
        }
    }

    private static void AddPersistingChangeObject(Transform trans) {
        var context = new MAObjectContext();
        allChangePersisters.Add(context);
        context.SetContext(trans);
    }

    private static void PlaymodeStateChanged() {
        if (EditorApplication.isPlaying || EditorApplication.isPaused) {
            if (allChangePersisters.Count == 0) {
                AddAllChangePersistingObjects();
            }

            foreach (var t in allChangePersisters) {
                t.GameObjectSetting.StoreAllSelectedSettings();
            }

            return;
        }

        // pressed stop
        if (!_persistChanges || allChangePersisters.Count == 0) {
            allChangePersisters.Clear(); // make sure we don't accidentally restore while entering Play mode.
            return;
        }

        foreach (var t in allChangePersisters) {
            var listOfComponentsChanged = t.GameObjectSetting.RestoreAllSelectedSettings();

            if (listOfComponentsChanged.Count <= 0) {
                continue;
            }

            var prefabType = PrefabUtility.GetPrefabType(t.GameObj);
            if (prefabType != PrefabType.DisconnectedPrefabInstance && prefabType != PrefabType.PrefabInstance) {
                continue;
            }

            foreach (var changedComp in listOfComponentsChanged) {
                EditorUtility.SetDirty(changedComp);
            }
        }

        allChangePersisters.Clear();
    }
    #endregion

    public override void OnInspectorGUI() {
        EditorGUIUtility.LookLikeControls();
        EditorGUI.indentLevel = 0;

        _sounds = (MasterAudio)target;

        if (MasterAudioInspectorResources.LogoTexture != null) {
            DTGUIHelper.ShowHeaderTexture(MasterAudioInspectorResources.LogoTexture);
        }

        ScanGroups();

        if (!_isValid) {
            return;
        }

        if (_sounds.transform.parent != null) {
            DTGUIHelper.ShowRedError("You have the Master Audio game object as a child of another game object.");
            DTGUIHelper.ShowRedError("This is not a supported scenario and some things might break. Please unparent it.");
        }

        _isDirty = false;
        _previewer = _sounds.gameObject;

        var sliderIndicatorChars = 6;
        var sliderWidth = 40;

        if (MasterAudio.UseDbScaleForVolume) {
            sliderIndicatorChars = 9;
            sliderWidth = 56;
        }

        var fakeDirty = false;
        var isInProjectView = DTGUIHelper.IsPrefabInProjectView(_sounds);

        _playlistNames = new List<string>();

        var maxPlaylistNameChars = 11;
        foreach (var t in _sounds.musicPlaylists) {
            var pList = t;

            _playlistNames.Add(pList.playlistName);
            if (pList.playlistName.Length > maxPlaylistNameChars) {
                maxPlaylistNameChars = pList.playlistName.Length;
            }
        }

        var groupNameList = GroupNameList;

        var busFilterList = new List<string> { MasterAudio.AllBusesName, MasterAudioGroup.NoBus };

        var maxChars = 9;
        var busList = new List<string> { MasterAudioGroup.NoBus, NewBusName };

        var busVoiceLimitList = new List<string> { MasterAudio.NoVoiceLimitName };

        for (var i = 1; i <= 32; i++) {
            busVoiceLimitList.Add(i.ToString());
        }

        foreach (var t in _sounds.groupBuses) {
            var bus = t;
            busList.Add(bus.busName);
            busFilterList.Add(bus.busName);

            if (bus.busName.Length > maxChars) {
                maxChars = bus.busName.Length;
            }
        }
        var busListWidth = 9 * maxChars;
        var playlistListWidth = 9 * maxPlaylistNameChars;
        var extraPlaylistLength = 0;
        if (maxPlaylistNameChars > 11) {
            extraPlaylistLength = 9 * (11 - maxPlaylistNameChars);
        }

        PlaylistController.Instances = null;
        var pcs = PlaylistController.Instances;
        var plControllerInScene = pcs.Count > 0;

        // mixer master volume!
        EditorGUILayout.BeginHorizontal();
        var volumeBefore = _sounds._masterAudioVolume;
        GUILayout.Label(DTGUIHelper.LabelVolumeField("Master Mixer Volume"), GUILayout.Width(163));
        GUILayout.Space(5);

        var newMasterVol = DTGUIHelper.DisplayVolumeField(_sounds._masterAudioVolume, DTGUIHelper.VolumeFieldType.None, false);
        if (newMasterVol != _sounds._masterAudioVolume) {
            UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Master Mixer Volume");
            if (Application.isPlaying) {
                MasterAudio.MasterVolumeLevel = newMasterVol;
            } else {
                _sounds._masterAudioVolume = newMasterVol;
            }
        }
        GUILayout.Space(11);

        var mixerMuteButtonPressed = DTGUIHelper.AddMixerMuteButton("Mixer", _sounds);

        GUILayout.FlexibleSpace();

        if (mixerMuteButtonPressed == DTGUIHelper.DTFunctionButtons.Mute) {
            UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Mixer Mute");

            _sounds.mixerMuted = !_sounds.mixerMuted;
            if (Application.isPlaying) {
                MasterAudio.MixerMuted = _sounds.mixerMuted;
            } else {
                foreach (var aGroup in groups) {
                    aGroup.isMuted = _sounds.mixerMuted;
                    if (aGroup.isMuted) {
                        aGroup.isSoloed = false;
                    }
                }
            }
        }

        EditorGUILayout.EndHorizontal();

        if (volumeBefore != _sounds._masterAudioVolume) {
            // fix it for realtime adjustments!
            MasterAudio.MasterVolumeLevel = _sounds._masterAudioVolume;
        }

        // playlist master volume!
        if (plControllerInScene) {
            EditorGUILayout.BeginHorizontal();
            GUILayout.Label(DTGUIHelper.LabelVolumeField("Master Playlist Volume"), GUILayout.Width(163));
            GUILayout.Space(5);
            var newPlaylistVol = DTGUIHelper.DisplayVolumeField(_sounds._masterPlaylistVolume, DTGUIHelper.VolumeFieldType.None, false);
            if (newPlaylistVol != _sounds._masterPlaylistVolume) {
                UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Master Playlist Volume");
                if (Application.isPlaying) {
                    MasterAudio.PlaylistMasterVolume = newPlaylistVol;
                } else {
                    _sounds._masterPlaylistVolume = newPlaylistVol;
                }
            }
            GUILayout.Space(11);
            var playlistMuteButtonPressed = DTGUIHelper.AddPlaylistMuteButton("All Playlists", _sounds);
            if (playlistMuteButtonPressed == DTGUIHelper.DTFunctionButtons.Mute) {
                if (Application.isPlaying) {
                    MasterAudio.PlaylistsMuted = !MasterAudio.PlaylistsMuted;
                } else {
                    _sounds.playlistsMuted = !_sounds.playlistsMuted;

                    foreach (var t in pcs) {
                        if (_sounds.playlistsMuted) {
                            t.MutePlaylist();
                        } else {
                            t.UnmutePlaylist();
                        }
                    }
                }
            }

            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Master Crossfade Time", GUILayout.Width(163));

            GUILayout.Space(5);
            var newCrossTime = EditorGUILayout.Slider(_sounds.crossFadeTime, 0f, 10f, GUILayout.Width(252));
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (newCrossTime != _sounds.crossFadeTime) {
                UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Master Crossfade Time");
                _sounds.crossFadeTime = newCrossTime;
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // jukebox controls
            if (Application.isPlaying) {
                DisplayJukebox();
            }
        }

        // Localization section Start
        EditorGUI.indentLevel = 0;

        var state = _sounds.showLocalization;
        var text = "Languages";

        // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
        if (!state) {
            GUI.backgroundColor = DTGUIHelper.InactiveHeaderColor;
        } else {
            GUI.backgroundColor = DTGUIHelper.ActiveHeaderColor;
        }

        GUILayout.BeginHorizontal();

#if UNITY_3_5_7
        if (!state) {
            text += " (Click to expand)";
        }
#else
        text = "<b><size=11>" + text + "</size></b>";
#endif
        if (state) {
            text = "\u25BC " + text;
        } else {
            text = "\u25BA " + text;
        }
        if (!GUILayout.Toggle(true, text, "dragtab", GUILayout.MinWidth(20f))) {
            state = !state;
        }

        GUILayout.Space(2f);

        if (state != _sounds.showLocalization) {
            UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Languages");
            _sounds.showLocalization = state;
        }

        EditorGUILayout.EndHorizontal();
        GUI.color = Color.white;

        if (_sounds.showLocalization) {
            DTGUIHelper.BeginGroupedControls();
            if (Application.isPlaying) {
                DTGUIHelper.ShowColorWarning("*Language settings cannot be changed during runtime");
            } else {
                int? langToRemove = null;
                int? langToAdd = null;

                for (var i = 0; i < _sounds.supportedLanguages.Count; i++) {
                    var aLang = _sounds.supportedLanguages[i];

                    DTGUIHelper.StartGroupHeader();
                    EditorGUILayout.BeginHorizontal();

                    EditorGUILayout.LabelField("Supported Lang. " + (i + 1), GUILayout.Width(142));
                    var newLang = (SystemLanguage)EditorGUILayout.EnumPopup("", aLang);
                    if (newLang != aLang) {
                        UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Supported Language");
                        _sounds.supportedLanguages[i] = newLang;
                    }
                    GUILayout.FlexibleSpace();

                    var buttonPressed = DTGUIHelper.AddFoldOutListItemButtons(i, _sounds.supportedLanguages.Count, "Supported Language", true);

                    switch (buttonPressed) {
                        case DTGUIHelper.DTFunctionButtons.Remove:
                            langToRemove = i;
                            break;
                        case DTGUIHelper.DTFunctionButtons.Add:
                            langToAdd = i;
                            break;
                    }

                    EditorGUILayout.EndHorizontal();
                    DTGUIHelper.EndGroupHeader();
                    DTGUIHelper.AddSpaceForNonU5(2);
                }

                if (langToAdd.HasValue) {
                    _sounds.supportedLanguages.Insert(langToAdd.Value + 1, SystemLanguage.Unknown);
                } else if (langToRemove.HasValue) {
                    if (_sounds.supportedLanguages.Count <= 1) {
                        DTGUIHelper.ShowAlert("You cannot delete the last Supported Language, although you do not have to use Localization.");
                    } else {
                        UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "Delete Supported Language");
                        _sounds.supportedLanguages.RemoveAt(langToRemove.Value);
                    }
                }

                if (!_sounds.supportedLanguages.Contains(_sounds.defaultLanguage)) {
                    DTGUIHelper.ShowLargeBarAlert("Please add your default language under Supported Languages as well.");
                }

                var newLang2 = (SystemLanguage)EditorGUILayout.EnumPopup(new GUIContent("Default Language", "This language will be used if the user's current language is not supported."), _sounds.defaultLanguage);
                if (newLang2 != _sounds.defaultLanguage) {
                    UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Default Language");
                    _sounds.defaultLanguage = newLang2;
                }

                var newMode = (MasterAudio.LanguageMode)EditorGUILayout.EnumPopup("Language Mode", _sounds.langMode);
                if (newMode != _sounds.langMode) {
                    UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Language Mode");
                    _sounds.langMode = newMode;
                    AudioResourceOptimizer.ClearSupportLanguageFolder();
                }

                if (_sounds.langMode == MasterAudio.LanguageMode.SpecificLanguage) {
                    var newLang = (SystemLanguage)EditorGUILayout.EnumPopup(new GUIContent("Use Specific Language", "This language will be used instead of your computer's setting. This is useful for testing other languages."), _sounds.testLanguage);
                    if (newLang != _sounds.testLanguage) {
                        UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Use Specific");
                        _sounds.testLanguage = newLang;
                        AudioResourceOptimizer.ClearSupportLanguageFolder();
                    }

                    if (_sounds.supportedLanguages.Contains(_sounds.testLanguage)) {
                        DTGUIHelper.ShowLargeBarAlert("Please select your Specific Language under Supported Languages as well.");
                        DTGUIHelper.ShowLargeBarAlert("If you do not, it will use your Default Language instead.");
                    }
                } else if (_sounds.langMode == MasterAudio.LanguageMode.DynamicallySet) {
                    DTGUIHelper.ShowLargeBarAlert("Dynamic Language currently set to: " + MasterAudio.DynamicLanguage.ToString());
                }
            }
            DTGUIHelper.EndGroupedControls();
        }

        // Advanced section Start
        DTGUIHelper.VerticalSpace(3);

        EditorGUI.indentLevel = 0;

        state = _sounds.showAdvancedSettings;
        text = "Advanced Settings";

        // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
        if (!state) {
            GUI.backgroundColor = DTGUIHelper.InactiveHeaderColor;
        } else {
            GUI.backgroundColor = DTGUIHelper.ActiveHeaderColor;
        }

        GUILayout.BeginHorizontal();

#if UNITY_3_5_7
        if (!state) {
            text += " (Click to expand)";
        }
#else
        text = "<b><size=11>" + text + "</size></b>";
#endif
        if (state) {
            text = "\u25BC " + text;
        } else {
            text = "\u25BA " + text;
        }
        if (!GUILayout.Toggle(true, text, "dragtab", GUILayout.MinWidth(20f))) {
            state = !state;
        }

        GUILayout.Space(2f);

        if (state != _sounds.showAdvancedSettings) {
            UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Advanced Settings");
            _sounds.showAdvancedSettings = state;
        }
        EditorGUILayout.EndHorizontal();
        GUI.color = Color.white;

        if (_sounds.showAdvancedSettings) {
            DTGUIHelper.BeginGroupedControls();
            if (!Application.isPlaying) {
                var newPersist = EditorGUILayout.Toggle(new GUIContent("Persist Across Scenes", "Turn this on only if you need music or other sounds to play across Scene changes. If not, create a different Master Audio prefab in each Scene."), _sounds.persistBetweenScenes);
                if (newPersist != _sounds.persistBetweenScenes) {
                    UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Persist Across Scenes");
                    _sounds.persistBetweenScenes = newPersist;
                }
            }

            if (_sounds.persistBetweenScenes && plControllerInScene) {
                DTGUIHelper.ShowColorWarning("*Playlist Controller will also persist between scenes!");
            }

#if UNITY_3_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5
            // no support
#else
            var newGap = EditorGUILayout.Toggle(new GUIContent("Gapless Music Switching", "Turn this option on if you need perfect gapless transitions between clips in your Playlists. For Resource Files, this will take more audio memory as 2 clips are generally loaded into memory at the same time, so only use it if you need to."), _sounds.useGaplessPlaylists);
            if (newGap != _sounds.useGaplessPlaylists) {
                UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Gapless Music Switching");
                _sounds.useGaplessPlaylists = newGap;
            }
#endif

            var newSave = EditorGUILayout.Toggle(new GUIContent("Save Runtime Changes", "Turn this on if you want to do real time adjustments to the mix, Master Audio prefab, Groups and Playlist Controllers and have the changes stick after you stop playing."), _sounds.saveRuntimeChanges);
            if (newSave != _sounds.saveRuntimeChanges) {
                UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Save Runtime Changes");
                _sounds.saveRuntimeChanges = newSave;
                _persistChanges = newSave;
            }

            var newIgnore = EditorGUILayout.Toggle(new GUIContent("Ignore Time Scale", "Turn this option on if you find that Master Audio features you are using do not work when Time Scale is set to zero. This does take a bit of extra performance away, so don't use unless necessary."), _sounds.ignoreTimeScale);
            if (newIgnore != _sounds.ignoreTimeScale) {
                UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Ignore Time Scale");
                _sounds.ignoreTimeScale = newIgnore;
            }

            DTGUIHelper.StartGroupHeader();
            var newAutoPrioritize = GUILayout.Toggle(_sounds.prioritizeOnDistance, new GUIContent(" Apply Distance Priority", "Turn this on to have Master Audio automatically assign Priority to all audio, based on distance from the Audio Listener. Playlist Controller and 2D sounds are unaffected."));
            if (newAutoPrioritize != _sounds.prioritizeOnDistance) {
                UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Prioritize By Distance");
                _sounds.prioritizeOnDistance = newAutoPrioritize;
            }
            EditorGUILayout.EndVertical();

            if (_sounds.prioritizeOnDistance) {
                EditorGUI.indentLevel = 0;

                var reevalIndex = _sounds.rePrioritizeEverySecIndex;

                var evalTimes = new List<string>();
                foreach (var t in _reevaluatePriorityTimes) {
                    evalTimes.Add(t + " seconds");
                }

                var newRepri = EditorGUILayout.Popup("Reprioritize Time Gap", reevalIndex, evalTimes.ToArray());
                if (newRepri != reevalIndex) {
                    UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Re-evaluate time");
                    _sounds.rePrioritizeEverySecIndex = newRepri;
                }

                var newContinual = EditorGUILayout.Toggle("Use Clip Age Priority", _sounds.useClipAgePriority);
                if (newContinual != _sounds.useClipAgePriority) {
                    UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Use Clip Age Priority");
                    _sounds.useClipAgePriority = newContinual;
                }
            }
            EditorGUILayout.EndVertical();

            EditorGUI.indentLevel = 1;

            DTGUIHelper.AddSpaceForNonU5(2);

            DTGUIHelper.StartGroupHeader();

            var newVisual = DTGUIHelper.Foldout(_sounds.visualAdvancedExpanded, "Visual Settings");
            if (newVisual != _sounds.visualAdvancedExpanded) {
                UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Visual Settings");
                _sounds.visualAdvancedExpanded = newVisual;
            }
            EditorGUILayout.EndVertical();

            if (_sounds.visualAdvancedExpanded) {
                EditorGUI.indentLevel = 0;

                var newGiz = EditorGUILayout.Toggle(new GUIContent("Show Variation Gizmos", "Turning this option on will show you where your Variation and Event Sounds components are in the Scene with an 'M' icon."), _sounds.showGizmos);
                if (newGiz != _sounds.showGizmos) {
                    UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Show Variation Gizmos");
                    _sounds.showGizmos = newGiz;
                }
            }
            EditorGUILayout.EndVertical();

            EditorGUI.indentLevel = 1;
            DTGUIHelper.AddSpaceForNonU5(2);
            DTGUIHelper.StartGroupHeader();
            var exp = DTGUIHelper.Foldout(_sounds.showFadingSettings, "Fading Settings");
            if (exp != _sounds.showFadingSettings) {
                UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Fading Settings");
                _sounds.showFadingSettings = exp;
            }
            EditorGUILayout.EndVertical();
            if (_sounds.showFadingSettings) {
                EditorGUI.indentLevel = 0;
                DTGUIHelper.ShowLargeBarAlert("Fading to zero volume on the following causes their audio to stop (if checked).");

                var newStop = EditorGUILayout.Toggle("Buses", _sounds.stopZeroVolumeBuses);
                if (newStop != _sounds.stopZeroVolumeBuses) {
                    UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Buses Stop");
                    _sounds.stopZeroVolumeBuses = newStop;
                }

                newStop = EditorGUILayout.Toggle("Variations", _sounds.stopZeroVolumeVariations);
                if (newStop != _sounds.stopZeroVolumeVariations) {
                    UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Variations Stop");
                    _sounds.stopZeroVolumeVariations = newStop;
                }

                newStop = EditorGUILayout.Toggle("Sound Groups", _sounds.stopZeroVolumeGroups);
                if (newStop != _sounds.stopZeroVolumeGroups) {
                    UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Sound Groups Stop");
                    _sounds.stopZeroVolumeGroups = newStop;
                }

                newStop = EditorGUILayout.Toggle(new GUIContent("Playlist Controllers", "Automatic crossfading will not trigger stop."), _sounds.stopZeroVolumePlaylists);
                if (newStop != _sounds.stopZeroVolumePlaylists) {
                    UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Playlist Controllers Stop");
                    _sounds.stopZeroVolumePlaylists = newStop;
                }
            }
            EditorGUILayout.EndVertical();

            EditorGUI.indentLevel = 1;
            DTGUIHelper.AddSpaceForNonU5(2);
            DTGUIHelper.StartGroupHeader();
            var newResource = DTGUIHelper.Foldout(_sounds.resourceAdvancedExpanded, "Resource File Settings");
            if (newResource != _sounds.resourceAdvancedExpanded) {
                UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Resource File Settings");
                _sounds.resourceAdvancedExpanded = newResource;
            }
            EditorGUILayout.EndVertical();

            if (_sounds.resourceAdvancedExpanded) {
                EditorGUI.indentLevel = 0;
                if (MasterAudio.HasAsyncResourceLoaderFeature()) {
                    var newAsync = EditorGUILayout.Toggle(new GUIContent("Always Load Res. Async", "Checking this means that you will not have this control per Sound Group or Playlist. All Resource files will be loaded asynchronously."), _sounds.resourceClipsAllLoadAsync);
                    if (newAsync != _sounds.resourceClipsAllLoadAsync) {
                        UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Always Load Resources Async");
                        _sounds.resourceClipsAllLoadAsync = newAsync;
                    }
                }

                var newResourcePause = EditorGUILayout.Toggle(new GUIContent("Keep Paused Resources", "If you check this box, Resource files will not be automatically unloaded from memory when you pause them. Use at your own risk!"), _sounds.resourceClipsPauseDoNotUnload);
                if (newResourcePause != _sounds.resourceClipsPauseDoNotUnload) {
                    UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Keep Paused Resources");
                    _sounds.resourceClipsPauseDoNotUnload = newResourcePause;
                }
            }
            EditorGUILayout.EndVertical();

            EditorGUI.indentLevel = 1;
            DTGUIHelper.AddSpaceForNonU5(2);
            DTGUIHelper.StartGroupHeader();
            var newLog = DTGUIHelper.Foldout(_sounds.logAdvancedExpanded, "Logging Settings");
            if (newLog != _sounds.logAdvancedExpanded) {
                UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Logging Settings");
                _sounds.logAdvancedExpanded = newLog;
            }
            EditorGUILayout.EndVertical();

            if (_sounds.logAdvancedExpanded) {
                EditorGUI.indentLevel = 0;
                newLog = EditorGUILayout.Toggle("Disable Logging", _sounds.disableLogging);
                if (newLog != _sounds.disableLogging) {
                    UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Disable Logging");
                    _sounds.disableLogging = newLog;
                }

                if (!_sounds.disableLogging) {
                    newLog = EditorGUILayout.Toggle("Log Sounds", _sounds.LogSounds);
                    if (newLog != _sounds.LogSounds) {
                        UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Log Sounds");
                        if (Application.isPlaying) {
                            MasterAudio.LogSoundsEnabled = _sounds.LogSounds;
                        }
                        _sounds.LogSounds = newLog;
                    }

                    newLog = EditorGUILayout.Toggle("Log Custom Events", _sounds.logCustomEvents);
                    if (newLog != _sounds.logCustomEvents) {
                        UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Log Custom Events");
                        _sounds.logCustomEvents = newLog;
                    }
                } else {
                    DTGUIHelper.ShowLargeBarAlert("Logging is disabled.");
                }
            }
            EditorGUILayout.EndVertical();

            DTGUIHelper.EndGroupedControls();
        }

        GUI.color = Color.white;
        GUI.backgroundColor = Color.white;
        GUI.contentColor = Color.white;
        // Music Ducking Start

        DTGUIHelper.VerticalSpace(3);

        EditorGUI.indentLevel = 0;

        state = _sounds.showMusicDucking;
        text = "Music Ducking";

        // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
        if (!state) {
            GUI.backgroundColor = DTGUIHelper.InactiveHeaderColor;
        } else {
            GUI.backgroundColor = DTGUIHelper.ActiveHeaderColor;
        }

        GUILayout.BeginHorizontal();

#if UNITY_3_5_7
        if (!state) {
            text += " (Click to expand)";
        }
#else
        text = "<b><size=11>" + text + "</size></b>";
#endif
        if (state) {
            text = "\u25BC " + text;
        } else {
            text = "\u25BA " + text;
        }
        if (!GUILayout.Toggle(true, text, "dragtab", GUILayout.MinWidth(20f))) {
            state = !state;
        }

        GUILayout.Space(2f);

        if (state != _sounds.showMusicDucking) {
            UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Show Music Ducking");
            _sounds.showMusicDucking = state;
        }
        EditorGUILayout.EndHorizontal();
        GUI.color = Color.white;

        if (_sounds.showMusicDucking) {
            DTGUIHelper.BeginGroupedControls();
            var newEnableDuck = EditorGUILayout.BeginToggleGroup("Enable Ducking", _sounds.EnableMusicDucking);
            if (newEnableDuck != _sounds.EnableMusicDucking) {
                UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Enable Ducking");
                _sounds.EnableMusicDucking = newEnableDuck;
            }

            EditorGUILayout.Separator();

            var newMult = EditorGUILayout.Slider("Ducked Vol Multiplier", _sounds.duckedVolumeMultiplier, 0f, 1f);
            if (newMult != _sounds.duckedVolumeMultiplier) {
                UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Ducked Vol Multiplier");
                _sounds.DuckedVolumeMultiplier = newMult;
            }

            var newDefault = EditorGUILayout.Slider("Default Begin Unduck", _sounds.defaultRiseVolStart, 0f, 1f);
            if (newDefault != _sounds.defaultRiseVolStart) {
                UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Default Begin Unduck");
                _sounds.defaultRiseVolStart = newDefault;
            }

            GUI.contentColor = DTGUIHelper.BrightButtonColor;
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(10);

            if (GUILayout.Button(new GUIContent("Add Duck Group"), EditorStyles.toolbarButton, GUILayout.Width(100))) {
                UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "Add Duck Group");
                _sounds.musicDuckingSounds.Add(new DuckGroupInfo() {
                    soundType = MasterAudio.NoGroupName,
                    riseVolStart = _sounds.defaultRiseVolStart
                });
            }

            EditorGUILayout.EndHorizontal();
            GUI.contentColor = Color.white;
            EditorGUILayout.Separator();

            if (_sounds.musicDuckingSounds.Count == 0) {
                DTGUIHelper.ShowLargeBarAlert("You currently have no ducking sounds set up.");
            } else {
                int? duckSoundToRemove = null;

                for (var i = 0; i < _sounds.musicDuckingSounds.Count; i++) {
                    var duckSound = _sounds.musicDuckingSounds[i];
                    var index = groupNameList.IndexOf(duckSound.soundType);
                    if (index == -1) {
                        index = 0;
                    }

                    DTGUIHelper.StartGroupHeader();

                    EditorGUILayout.BeginHorizontal();
                    var newIndex = EditorGUILayout.Popup(index, groupNameList.ToArray(), GUILayout.MaxWidth(200));
                    if (newIndex >= 0) {
                        if (index != newIndex) {
                            UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Duck Group");
                        }
                        duckSound.soundType = groupNameList[newIndex];
                    }

                    GUI.contentColor = DTGUIHelper.BrightButtonColor;
                    GUILayout.TextField("Begin Unduck " + duckSound.riseVolStart.ToString("N2"), 20, EditorStyles.miniLabel);

                    var newUnduck = GUILayout.HorizontalSlider(duckSound.riseVolStart, 0f, 1f, GUILayout.Width(60));
                    if (newUnduck != duckSound.riseVolStart) {
                        UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Begin Unduck");
                        duckSound.riseVolStart = newUnduck;
                    }
                    GUI.contentColor = Color.white;

                    GUILayout.FlexibleSpace();
                    GUILayout.Space(10);
                    if (DTGUIHelper.AddDeleteIcon("Duck Sound")) {
                        duckSoundToRemove = i;
                    }

                    EditorGUILayout.EndHorizontal();
                    DTGUIHelper.EndGroupHeader();
                    DTGUIHelper.AddSpaceForNonU5(2);
                }

                if (duckSoundToRemove.HasValue) {
                    UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "delete Duck Group");
                    _sounds.musicDuckingSounds.RemoveAt(duckSoundToRemove.Value);
                }

            }
            EditorGUILayout.EndToggleGroup();

            DTGUIHelper.EndGroupedControls();
        }
        // Music Ducking End

        GUI.color = Color.white;
        GUI.backgroundColor = Color.white;
        GUI.contentColor = Color.white;
        // Sound Groups Start		
        EditorGUI.indentLevel = 0;  // Space will handle this for the header
        DTGUIHelper.VerticalSpace(3);

        state = _sounds.areGroupsExpanded;
        text = "Group Mixer";

        // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
        if (!state) {
            GUI.backgroundColor = DTGUIHelper.InactiveHeaderColor;
        } else {
            GUI.backgroundColor = DTGUIHelper.ActiveHeaderColor;
        }

        GUILayout.BeginHorizontal();

#if UNITY_3_5_7
        if (!state) {
            text += " (Click to expand)";
        }
#else
        text = "<b><size=11>" + text + "</size></b>";
#endif
        if (state) {
            text = "\u25BC " + text;
        } else {
            text = "\u25BA " + text;
        }
        if (!GUILayout.Toggle(true, text, "dragtab", GUILayout.MinWidth(20f))) {
            state = !state;
        }

        GUILayout.Space(2f);

        if (state != _sounds.areGroupsExpanded) {
            UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Group Mixer");
            _sounds.areGroupsExpanded = state;
        }

        GUI.color = Color.white;
        EditorGUILayout.EndHorizontal();

        GameObject groupToDelete = null;
        // ReSharper disable once TooWideLocalVariableScope
        // ReSharper disable once RedundantAssignment
        var audSrcTemplateIndex = -1;
        var applyTemplateToAll = false;

        if (_sounds.areGroupsExpanded) {
            DTGUIHelper.BeginGroupedControls();

            EditorGUI.indentLevel = 1;

            DTGUIHelper.StartGroupHeader();

            var newShow = DTGUIHelper.Foldout(_sounds.showGroupCreation, "Group Creation");
            if (newShow != _sounds.showGroupCreation) {
                UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Group Creation");
                _sounds.showGroupCreation = !_sounds.showGroupCreation;
            }

            GUI.contentColor = Color.white;
            EditorGUILayout.EndVertical();

            if (_sounds.showGroupCreation) {
                EditorGUI.indentLevel = 0;
                var groupTemplateIndex = -1;

                if (_sounds.audioSourceTemplates.Count > 0 && !Application.isPlaying && _sounds.showGroupCreation && _sounds.transform.childCount > 0) {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(10);
                    GUI.contentColor = DTGUIHelper.BrightButtonColor;
                    if (GUILayout.Button("Apply Audio Source Template to All", EditorStyles.toolbarButton, GUILayout.Width(190))) {
                        applyTemplateToAll = true;
                    }
                    GUI.contentColor = Color.white;
                    EditorGUILayout.EndHorizontal();
                }

                var newTemp = EditorGUILayout.Toggle("Use Group Templates", _sounds.useGroupTemplates);
                if (newTemp != _sounds.useGroupTemplates) {
                    UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Use Group Templates");
                    _sounds.useGroupTemplates = newTemp;
                }

                if (_sounds.useGroupTemplates) {
                    _groupTemplateNames = new List<string>();

                    foreach (var temp in _sounds.groupTemplates) {
                        if (temp == null) {
                            continue;
                        }
                        _groupTemplateNames.Add(temp.name);
                    }

                    var templatesMissing = false;

                    if (Directory.Exists(GroupTemplateFolder)) {
                        var grpTemplates = Directory.GetFiles(GroupTemplateFolder, "*.prefab").Length;
                        if (grpTemplates > _sounds.groupTemplates.Count) {
                            templatesMissing = true;
                            DTGUIHelper.ShowLargeBarAlert("There's " + (grpTemplates - _sounds.groupTemplates.Count) + " Group Template(s) that aren't set up in this MA prefab. Locate them");
                            DTGUIHelper.ShowLargeBarAlert("in DarkTonic/MasterAudio/Sources/Prefabs/GroupTemplates and drag them in below.");
                        }
                    }

                    if (templatesMissing) {
                        // create groups start
                        EditorGUILayout.BeginVertical();
                        var aEvent = Event.current;

                        GUI.color = DTGUIHelper.DragAreaColor;

                        var dragArea = GUILayoutUtility.GetRect(0f, 35f, GUILayout.ExpandWidth(true));
                        GUI.Box(dragArea, "Drag prefabs here from Project View to create Group Templates!");

                        GUI.color = Color.white;

                        switch (aEvent.type) {
                            case EventType.DragUpdated:
                            case EventType.DragPerform:
                                if (!dragArea.Contains(aEvent.mousePosition)) {
                                    break;
                                }

                                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                                if (aEvent.type == EventType.DragPerform) {
                                    DragAndDrop.AcceptDrag();

                                    foreach (var dragged in DragAndDrop.objectReferences) {
                                        var temp = dragged as GameObject;
                                        if (temp == null) {
                                            continue;
                                        }

                                        AddGroupTemplate(temp);
                                    }
                                }
                                Event.current.Use();
                                break;
                        }
                        EditorGUILayout.EndVertical();
                        // create groups end
                    }

                    if (_groupTemplateNames.Count == 0) {
                        DTGUIHelper.ShowRedError("You cannot create Groups without Templates. Drag them in or disable Group Templates.");
                    } else {
                        groupTemplateIndex = _groupTemplateNames.IndexOf(_sounds.groupTemplateName);
                        if (groupTemplateIndex < 0) {
                            groupTemplateIndex = 0;
                            _sounds.groupTemplateName = _groupTemplateNames[0];
                        }

                        var newIndex = EditorGUILayout.Popup("Group Template", groupTemplateIndex, _groupTemplateNames.ToArray());
                        if (newIndex != groupTemplateIndex) {
                            UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Group Template");
                            _sounds.groupTemplateName = _groupTemplateNames[newIndex];
                        }
                    }
                }

                _audioSourceTemplateNames = new List<string>();

                foreach (var temp in _sounds.audioSourceTemplates) {
                    if (temp == null) {
                        continue;
                    }
                    _audioSourceTemplateNames.Add(temp.name);
                }

                var audTemplatesMissing = false;

                if (Directory.Exists(MasterAudio.AudioSourceTemplateFolder)) {
                    var audioSrcTemplates = Directory.GetFiles(MasterAudio.AudioSourceTemplateFolder, "*.prefab").Length;
                    if (audioSrcTemplates > _sounds.audioSourceTemplates.Count) {
                        audTemplatesMissing = true;
                        DTGUIHelper.ShowLargeBarAlert("There's " + (audioSrcTemplates - _sounds.audioSourceTemplates.Count) + " Audio Source Template(s) that aren't set up in this MA prefab. Locate them");
                        DTGUIHelper.ShowLargeBarAlert("in DarkTonic/MasterAudio/Sources/Prefabs/AudioSourceTemplates and drag them in below.");
                    }
                }

                if (audTemplatesMissing) {
                    // create groups start
                    EditorGUILayout.BeginVertical();
                    var aEvent = Event.current;

                    GUI.color = DTGUIHelper.DragAreaColor;

                    var dragArea = GUILayoutUtility.GetRect(0f, 35f, GUILayout.ExpandWidth(true));
                    GUI.Box(dragArea, "Drag prefabs here from Project View to create Group Templates!");

                    GUI.color = Color.white;

                    switch (aEvent.type) {
                        case EventType.DragUpdated:
                        case EventType.DragPerform:
                            if (!dragArea.Contains(aEvent.mousePosition)) {
                                break;
                            }

                            DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                            if (aEvent.type == EventType.DragPerform) {
                                DragAndDrop.AcceptDrag();

                                foreach (var dragged in DragAndDrop.objectReferences) {
                                    var temp = dragged as GameObject;
                                    if (temp == null) {
                                        continue;
                                    }

                                    AddAudioSourceTemplate(temp);
                                }
                            }
                            Event.current.Use();
                            break;
                    }
                    EditorGUILayout.EndVertical();
                    // create groups end
                }

                if (_audioSourceTemplateNames.Count == 0) {
                    DTGUIHelper.ShowRedError("You have no Audio Source Templates. Drag them in to create them.");
                } else {
                    audSrcTemplateIndex = _audioSourceTemplateNames.IndexOf(_sounds.audioSourceTemplateName);
                    if (audSrcTemplateIndex < 0) {
                        audSrcTemplateIndex = 0;
                        _sounds.audioSourceTemplateName = _audioSourceTemplateNames[0];
                    }

                    var newIndex = EditorGUILayout.Popup("Audio Source Template", audSrcTemplateIndex, _audioSourceTemplateNames.ToArray());
                    if (newIndex != audSrcTemplateIndex) {
                        UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Audio Source Template");
                        _sounds.audioSourceTemplateName = _audioSourceTemplateNames[newIndex];
                    }
                }

                if (_sounds.useGroupTemplates) {
                    DTGUIHelper.ShowColorWarning("*Bulk Creation Mode is ONE GROUP PER CLIP when using Group Templates.");
                } else {
                    var newGroupMode = (MasterAudio.DragGroupMode)EditorGUILayout.EnumPopup("Bulk Creation Mode", _sounds.curDragGroupMode);
                    if (newGroupMode != _sounds.curDragGroupMode) {
                        UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Bulk Creation Mode");
                        _sounds.curDragGroupMode = newGroupMode;
                    }
                }

                var newBulkMode = (MasterAudio.AudioLocation)EditorGUILayout.EnumPopup("Variation Create Mode", _sounds.bulkLocationMode);
                if (newBulkMode != _sounds.bulkLocationMode) {
                    UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Bulk Variation Mode");
                    _sounds.bulkLocationMode = newBulkMode;
                }

                if (_sounds.bulkLocationMode == MasterAudio.AudioLocation.ResourceFile) {
                    DTGUIHelper.ShowColorWarning("*Resource mode: make sure to drag from Resource folders only.");
                }

                var cannotCreateGroups = _sounds.useGroupTemplates && _sounds.groupTemplates.Count == 0;
                MasterAudioGroup createdGroup = null;

                if (!cannotCreateGroups) {
                    // create groups start
                    var anEvent = Event.current;

                    GUI.color = DTGUIHelper.DragAreaColor;

                    var useGroupTemplate = _sounds.useGroupTemplates && groupTemplateIndex > -1;

                    if (isInProjectView) {
                        DTGUIHelper.ShowLargeBarAlert("You are in Project View and cannot create or navigate Groups.");
                        DTGUIHelper.ShowRedError("Create this prefab from Master Audio Manager window. Do not drag into Scene!");
                    } else {
                        EditorGUILayout.BeginVertical();
                        var dragArea = GUILayoutUtility.GetRect(0f, 35f, GUILayout.ExpandWidth(true));
                        GUI.Box(dragArea, "Drag Audio clips here to create Groups!");

                        GUI.color = Color.white;

                        switch (anEvent.type) {
                            case EventType.DragUpdated:
                            case EventType.DragPerform:
                                if (!dragArea.Contains(anEvent.mousePosition)) {
                                    break;
                                }

                                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                                if (anEvent.type == EventType.DragPerform) {
                                    DragAndDrop.AcceptDrag();

                                    Transform groupTrans = null;

                                    foreach (var dragged in DragAndDrop.objectReferences) {
                                        var aClip = dragged as AudioClip;
                                        if (aClip == null) {
                                            continue;
                                        }

                                        if (useGroupTemplate) {
                                            createdGroup = CreateSoundGroupFromTemplate(aClip, groupTemplateIndex);
                                            continue;
                                        }

                                        if (_sounds.curDragGroupMode == MasterAudio.DragGroupMode.OneGroupPerClip) {
                                            CreateSoundGroup(aClip);
                                        } else {
                                            if (groupTrans == null) { // one group with variations
                                                groupTrans = CreateSoundGroup(aClip);
                                            } else {
                                                CreateVariation(groupTrans, aClip);
                                                // create the variations
                                            }
                                        }
                                    }
                                }
                                Event.current.Use();
                                break;
                        }
                        EditorGUILayout.EndVertical();
                    }
                    // create groups end
                }

                if (createdGroup != null) {
                    MasterAudioGroupInspector.RescanChildren(createdGroup);
                }
            }
            EditorGUILayout.EndVertical();

            EditorGUI.indentLevel = 0;

            DTGUIHelper.AddSpaceForNonU5(2);
            DTGUIHelper.StartGroupHeader();

            EditorGUILayout.LabelField("Group Control");

            EditorGUILayout.EndVertical();

            DTGUIHelper.ResetColors();

#if UNITY_5_0
            var newSpatialType = (MasterAudio.AllMixerSpatialBlendType)EditorGUILayout.EnumPopup("Group Spatial Blend Rule", _sounds.mixerSpatialBlendType);
            if (newSpatialType != _sounds.mixerSpatialBlendType) {
                UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Group Spatial Blend Rule");
                _sounds.mixerSpatialBlendType = newSpatialType;

                if (Application.isPlaying) {
                    _sounds.SetSpatialBlendForMixer();
                }
            }

            switch (_sounds.mixerSpatialBlendType) {
                case MasterAudio.AllMixerSpatialBlendType.ForceAllToCustom:
                    EditorGUI.indentLevel = 1;
                    DTGUIHelper.ShowLargeBarAlert(SpatialBlendSliderText);
                    var newBlend = EditorGUILayout.Slider("Group Spatial Blend", _sounds.mixerSpatialBlend, 0f, 1f);
                    if (newBlend != _sounds.mixerSpatialBlend) {
                        UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Group Spatial Blend");
                        _sounds.mixerSpatialBlend = newBlend;
                        if (Application.isPlaying) {
                            _sounds.SetSpatialBlendForMixer();
                        }
                    }
                    break;
                case MasterAudio.AllMixerSpatialBlendType.AllowDifferentPerGroup:
                    EditorGUI.indentLevel = 1;
                    DTGUIHelper.ShowLargeBarAlert("Go to each Group's settings to change Spatial Blend. Defaults below are for new Groups.");

                    var newDefType = (MasterAudio.ItemSpatialBlendType)EditorGUILayout.EnumPopup("Default Blend Type", _sounds.newGroupSpatialType);
                    if (newDefType != _sounds.newGroupSpatialType) {
                        UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Default Blend Type");
                        _sounds.newGroupSpatialType = newDefType;
                    }

                    if (_sounds.newGroupSpatialType == MasterAudio.ItemSpatialBlendType.ForceToCustom) {
                        newBlend = EditorGUILayout.Slider("Default Spatial Blend", _sounds.newGroupSpatialBlend, 0f, 1f);
                        if (newBlend != _sounds.newGroupSpatialBlend) {
                            UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Default  Spatial Blend");
                            _sounds.newGroupSpatialBlend = newBlend;
                        }
                    }
                    break;
            }
#endif

            EditorGUI.indentLevel = 0;
            var newBusFilterIndex = -1;
            var busFilterActive = false;

            if (_sounds.groupBuses.Count > 0) {
                busFilterActive = true;
                var oldBusFilter = busFilterList.IndexOf(_sounds.busFilter);
                if (oldBusFilter == -1) {
                    oldBusFilter = 0;
                }

                newBusFilterIndex = EditorGUILayout.Popup("Bus Filter", oldBusFilter, busFilterList.ToArray());

                var newBusFilter = busFilterList[newBusFilterIndex];

                if (_sounds.busFilter != newBusFilter) {
                    UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Bus Filter");
                    _sounds.busFilter = newBusFilter;
                }
            }

            if (groups.Count > 0) {
                var newUseTextGroupFilter = EditorGUILayout.Toggle("Use Text Group Filter", _sounds.useTextGroupFilter);
                if (newUseTextGroupFilter != _sounds.useTextGroupFilter) {
                    UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Use Text Group Filter");
                    _sounds.useTextGroupFilter = newUseTextGroupFilter;
                }

                if (_sounds.useTextGroupFilter) {
                    EditorGUI.indentLevel = 1;

                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(10);
                    GUILayout.Label("Text Group Filter", GUILayout.Width(140));
                    var newTextFilter = GUILayout.TextField(_sounds.textGroupFilter, GUILayout.Width(180));
                    if (newTextFilter != _sounds.textGroupFilter) {
                        UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Text Group Filter");
                        _sounds.textGroupFilter = newTextFilter;
                    }
                    GUILayout.Space(10);
                    GUI.contentColor = DTGUIHelper.BrightButtonColor;
                    if (GUILayout.Button("Clear", EditorStyles.toolbarButton, GUILayout.Width(70))) {
                        _sounds.textGroupFilter = string.Empty;
                    }
                    GUI.contentColor = Color.white;
                    EditorGUILayout.EndHorizontal();

                    EditorGUILayout.Separator();
                }
            }

            EditorGUI.indentLevel = 0;
            var groupButtonPressed = DTGUIHelper.DTFunctionButtons.None;

            MasterAudioGroup aGroup = null;
            var filteredGroups = new List<MasterAudioGroup>();

            filteredGroups.AddRange(groups);

            if (busFilterActive && !string.IsNullOrEmpty(_sounds.busFilter)) {
                if (newBusFilterIndex == 0) {
                    // no filter
                } else if (newBusFilterIndex == 1) {
                    filteredGroups.RemoveAll(delegate(MasterAudioGroup obj) {
                        return obj.busIndex != 0;
                    });
                } else {
                    filteredGroups.RemoveAll(delegate(MasterAudioGroup obj) {
                        return obj.busIndex != newBusFilterIndex;
                    });
                }
            }

            if (_sounds.useTextGroupFilter) {
                if (!string.IsNullOrEmpty(_sounds.textGroupFilter)) {
                    filteredGroups.RemoveAll(delegate(MasterAudioGroup obj) {
                        return !obj.transform.name.ToLower().Contains(_sounds.textGroupFilter.ToLower());
                    });
                }
            }

            var totalVoiceCount = 0;

            var selectAll = false;
            var deselectAll = false;
            var applyAudioSourceTemplate = false;

            var bulkSelectedGrps = new List<MasterAudioGroup>(filteredGroups.Count);

            if (groups.Count == 0) {
                DTGUIHelper.ShowLargeBarAlert("You currently have zero Sound Groups.");
            } else {
                var groupsFiltered = groups.Count - filteredGroups.Count;
                if (groupsFiltered > 0) {
                    DTGUIHelper.ShowLargeBarAlert(string.Format("{0} Group(s) filtered out.", groupsFiltered));
                }

                if (_sounds.groupBuses.Count > 0) {
                    var newGroupByBus = EditorGUILayout.Toggle("Group by Bus", _sounds.groupByBus);
                    if (newGroupByBus != _sounds.groupByBus) {
                        UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Group by Bus");
                        _sounds.groupByBus = newGroupByBus;
                    }
                }

                if (filteredGroups.Count > 0) {
                    var newSelectGrp = EditorGUILayout.Toggle("Bulk Group Changes", _sounds.showGroupSelect);
                    if (newSelectGrp != _sounds.showGroupSelect) {
                        UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Bulk Group Changes");
                        _sounds.showGroupSelect = newSelectGrp;
                    }

                    if (_sounds.showGroupSelect) {
                        GUI.contentColor = DTGUIHelper.BrightButtonColor;
                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(10);
                        if (GUILayout.Button("Select All", EditorStyles.toolbarButton, GUILayout.Width(80))) {
                            selectAll = true;
                        }
                        GUILayout.Space(10);
                        if (GUILayout.Button("Deselect All", EditorStyles.toolbarButton, GUILayout.Width(80))) {
                            deselectAll = true;
                        }

                        foreach (var t in filteredGroups) {
                            if (t.isSelected) {
                                bulkSelectedGrps.Add(t);
                            }
                        }

                        if (!Application.isPlaying && _audioSourceTemplateNames.Count > 0 && bulkSelectedGrps.Count > 0) {
                            GUILayout.Space(10);
                            GUI.contentColor = DTGUIHelper.BrightButtonColor;
                            if (GUILayout.Button("Apply Audio Source Template", EditorStyles.toolbarButton, GUILayout.Width(160))) {
                                applyAudioSourceTemplate = true;
                            }
                        }
                        EditorGUILayout.EndHorizontal();
                        GUI.contentColor = Color.white;

                        if (bulkSelectedGrps.Count > 0) {
                            DTGUIHelper.ShowLargeBarAlert("Bulk editing - adjustments to a selected mixer Group will affect all " + bulkSelectedGrps.Count + " selected Group(s).");
                        } else {
                            EditorGUILayout.Separator();
                        }
                    }
                }

                var isBulkMute = false;
                var isBulkSolo = false;
                float? bulkVolume = null;
                int? bulkBusIndex = null;

                int? busToCreate = null;

                GUI.color = Color.white;
                GUI.backgroundColor = Color.white;
                GUI.contentColor = Color.white;

                for (var l = 0; l < filteredGroups.Count; l++) {
                    EditorGUI.indentLevel = 0;
                    aGroup = filteredGroups[l];

                    var groupDirty = false;
                    var isBulkEdit = bulkSelectedGrps.Count > 0 && aGroup.isSelected;

                    var sType = string.Empty;
                    if (Application.isPlaying) {
                        sType = aGroup.name;
                    }

                    EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
                    var groupName = aGroup.name;

                    if (Application.isPlaying) {
                        var groupVoices = aGroup.ActiveVoices;
                        var totalVoices = aGroup.TotalVoices;

                        GUI.color = DTGUIHelper.BrightTextColor;
                        if (groupVoices >= totalVoices) {
                            GUI.contentColor = Color.red;
                            GUI.backgroundColor = Color.red;
                        }

                        if (GUILayout.Button(new GUIContent(string.Format("[{0}]", groupVoices), "Click to select Variations"), EditorStyles.toolbarButton)) {
                            SelectActiveVariationsInGroup(aGroup);
                        }
                        GUI.color = Color.white;
                        GUI.contentColor = Color.white;
                        GUI.backgroundColor = Color.white;

                        totalVoiceCount += groupVoices;
                    }

                    if (_sounds.showGroupSelect) {
                        var newChecked = EditorGUILayout.Toggle(aGroup.isSelected, EditorStyles.toggleGroup, GUILayout.Width(16));
                        if (newChecked != aGroup.isSelected) {
                            UndoHelper.RecordObjectPropertyForUndo(ref groupDirty, aGroup, "toggle Select Group");
                            aGroup.isSelected = newChecked;
                        }
                    }

                    EditorGUILayout.LabelField(groupName, EditorStyles.label, GUILayout.MinWidth(100));

                    GUILayout.FlexibleSpace();
                    EditorGUILayout.BeginHorizontal(GUILayout.MaxWidth(50));

                    // find bus.
                    var selectedBusIndex = aGroup.busIndex == -1 ? 0 : aGroup.busIndex;

                    GUI.contentColor = Color.white;
                    GUI.color = DTGUIHelper.BrightButtonColor;

                    var busIndex = EditorGUILayout.Popup("", selectedBusIndex, busList.ToArray(), GUILayout.Width(busListWidth));
                    if (busIndex == -1) {
                        busIndex = 0;
                    }

                    if (aGroup.busIndex != busIndex && busIndex != 1) {
                        if (isBulkEdit) {
                            bulkBusIndex = busIndex;
                        } else {
                            UndoHelper.RecordObjectPropertyForUndo(ref groupDirty, aGroup, "change Group Bus");
                        }
                    }

                    if (busIndex != 1 && !isBulkEdit) { // don't change the index, so undo will work.
                        aGroup.busIndex = busIndex;
                    }

                    GUI.color = Color.white;

                    if (selectedBusIndex != busIndex) {
                        if (busIndex == 0 && Application.isPlaying) {
#if UNITY_5_0
                            _sounds.RouteGroupToUnityMixerGroup(sType, null);
#endif
                        } else if (busIndex == 1) {
                            busToCreate = l;
                            if (Application.isPlaying) {
#if UNITY_5_0
                                _sounds.RouteGroupToUnityMixerGroup(sType, null);
#endif
                            }
                        } else if (busIndex >= MasterAudio.HardCodedBusOptions) {
                            var newBus = _sounds.groupBuses[busIndex - MasterAudio.HardCodedBusOptions];
                            if (Application.isPlaying) {
                                var statGroup = MasterAudio.GrabGroup(sType);
                                statGroup.busIndex = busIndex;

                                if (newBus.isMuted) {
                                    MasterAudio.MuteGroup(aGroup.name);
                                } else if (newBus.isSoloed) {
                                    MasterAudio.SoloGroup(aGroup.name);
                                }

                                // reassign Unity Mixer Group
#if UNITY_5_0
                                _sounds.RouteGroupToUnityMixerGroup(sType, newBus.mixerChannel);
#endif
                            } else {
                                // check if bus soloed or muted.
                                if (newBus.isMuted) {
                                    aGroup.isMuted = true;
                                    aGroup.isSoloed = false;
                                } else if (newBus.isSoloed) {
                                    aGroup.isMuted = false;
                                    aGroup.isSoloed = true;
                                }
                            }
                        }
                    }

                    GUI.contentColor = DTGUIHelper.BrightTextColor;

                    GUILayout.TextField(DTGUIHelper.DisplayVolumeNumber(aGroup.groupMasterVolume, sliderIndicatorChars), sliderIndicatorChars, EditorStyles.miniLabel, GUILayout.Width(sliderWidth));

                    var newVol = DTGUIHelper.DisplayVolumeField(aGroup.groupMasterVolume, DTGUIHelper.VolumeFieldType.MixerGroup, _sounds.wideMixer);
                    if (newVol != aGroup.groupMasterVolume) {
                        if (isBulkEdit) {
                            bulkVolume = newVol;
                        } else {
                            SetMixerGroupVolume(aGroup, ref groupDirty, newVol, false);
                        }
                    }

                    GUI.contentColor = Color.white;
                    DTGUIHelper.AddLedSignalLight(_sounds, groupName);

                    groupButtonPressed = DTGUIHelper.AddMixerButtons(aGroup, "Group");

                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndHorizontal();

                    switch (groupButtonPressed) {
                        case DTGUIHelper.DTFunctionButtons.Play:
                            if (Application.isPlaying) {
                                MasterAudio.PlaySoundAndForget(aGroup.name);
                            } else {
                                var rndIndex = Random.Range(0, aGroup.groupVariations.Count);
                                var rndVar = aGroup.groupVariations[rndIndex];

                                var calcVolume = aGroup.groupMasterVolume * rndVar.VarAudio.volume;

                                if (rndVar.audLocation == MasterAudio.AudioLocation.ResourceFile) {
                                    StopPreviewer();
                                    var fileName = AudioResourceOptimizer.GetLocalizedFileName(rndVar.useLocalization, rndVar.resourceFileName);
                                    GetPreviewer().PlayOneShot(Resources.Load(fileName) as AudioClip, calcVolume);
                                } else {
                                    rndVar.VarAudio.PlayOneShot(rndVar.VarAudio.clip, calcVolume);
                                }

                                _isDirty = true;
                            }
                            break;
                        case DTGUIHelper.DTFunctionButtons.Stop:
                            if (Application.isPlaying) {
                                MasterAudio.StopAllOfSound(aGroup.name);
                            } else {
                                var hasResourceFile = false;
                                foreach (var t in aGroup.groupVariations) {
                                    t.VarAudio.Stop();
                                    if (t.audLocation == MasterAudio.AudioLocation.ResourceFile) {
                                        hasResourceFile = true;
                                    }
                                }

                                if (hasResourceFile) {
                                    StopPreviewer();
                                }
                            }
                            break;
                        case DTGUIHelper.DTFunctionButtons.Mute:
                            if (isBulkEdit) {
                                isBulkMute = true;
                            } else {
                                MuteMixerGroup(aGroup, ref groupDirty, false, false);
                            }
                            break;
                        case DTGUIHelper.DTFunctionButtons.Solo:
                            if (isBulkEdit) {
                                isBulkSolo = true;
                            } else {
                                SoloMixerGroup(aGroup, ref groupDirty, false, false);
                            }
                            break;
                        case DTGUIHelper.DTFunctionButtons.Go:
                            Selection.activeObject = aGroup.transform;
                            break;
                        case DTGUIHelper.DTFunctionButtons.Remove:
                            groupToDelete = aGroup.transform.gameObject;
                            break;
                    }

                    if (groupDirty) {
                        EditorUtility.SetDirty(aGroup);
                    }
                }

                if (isBulkMute) {
                    UndoHelper.RecordObjectsForUndo(bulkSelectedGrps.ToArray(), "Bulk Mute");

                    var wasWarningShown = false;

                    foreach (var grp in bulkSelectedGrps) {
                        wasWarningShown = MuteMixerGroup(grp, ref fakeDirty, true, wasWarningShown);
                        EditorUtility.SetDirty(grp);
                    }
                }

                if (isBulkSolo) {
                    UndoHelper.RecordObjectsForUndo(bulkSelectedGrps.ToArray(), "Bulk Solo");

                    var wasWarningShown = false;

                    foreach (var grp in bulkSelectedGrps) {
                        wasWarningShown = SoloMixerGroup(grp, ref fakeDirty, true, wasWarningShown);
                        EditorUtility.SetDirty(grp);
                    }
                }

                if (bulkVolume.HasValue) {
                    UndoHelper.RecordObjectsForUndo(bulkSelectedGrps.ToArray(), "Bulk Volume Adjustment");

                    foreach (var grp in bulkSelectedGrps) {
                        SetMixerGroupVolume(grp, ref fakeDirty, bulkVolume.Value, true);
                        EditorUtility.SetDirty(grp);
                    }
                }

                if (bulkBusIndex.HasValue) {
                    UndoHelper.RecordObjectsForUndo(bulkSelectedGrps.ToArray(), "Bulk Bus Assignment");


                    foreach (var grp in bulkSelectedGrps) {
                        grp.busIndex = bulkBusIndex.Value;
#if UNITY_5_0
                        if (bulkBusIndex.Value == 0) {
                            _sounds.RouteGroupToUnityMixerGroup(grp.name, null);
                        } else {
                            var theBus = _sounds.groupBuses[bulkBusIndex.Value - MasterAudio.HardCodedBusOptions];
                            if (theBus != null) {
                                _sounds.RouteGroupToUnityMixerGroup(grp.name, theBus.mixerChannel);
                            }
                        }
#endif

                        EditorUtility.SetDirty(grp);
                    }
                }

                if (busToCreate.HasValue) {
                    CreateBus(busToCreate.Value);
                }

                if (groupToDelete != null) {
                    _sounds.musicDuckingSounds.RemoveAll(delegate(DuckGroupInfo obj) {
                        return obj.soundType == groupToDelete.name;
                    });
                    UndoHelper.DestroyForUndo(groupToDelete);
                }

                if (Application.isPlaying) {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(9);
                    GUI.color = DTGUIHelper.BrightTextColor;
                    EditorGUILayout.LabelField(string.Format("[{0}] Total Active Voices", totalVoiceCount));
                    GUI.color = Color.white;
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Separator();
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(10);
                GUI.contentColor = DTGUIHelper.BrightButtonColor;
                if (GUILayout.Button(new GUIContent("Mute/Solo Reset", "Turn off all group mute and solo switches"), EditorStyles.toolbarButton, GUILayout.Width(120))) {
                    UndoHelper.RecordObjectsForUndo(groups.ToArray(), "Mute/Solo Reset");

                    foreach (var t in groups) {
                        aGroup = t;
                        aGroup.isSoloed = false;
                        aGroup.isMuted = false;
                    }
                }

                GUILayout.Space(6);

                if (GUILayout.Button(new GUIContent("Max Group Volumes", "Reset all group volumes to full"), EditorStyles.toolbarButton, GUILayout.Width(120))) {
                    UndoHelper.RecordObjectsForUndo(groups.ToArray(), "Max Group Volumes");

                    foreach (var t in groups) {
                        aGroup = t;
                        aGroup.groupMasterVolume = 1f;
                    }
                }

                GUILayout.Space(6);

                var buttonText = "Use Wide Mode";
                if (_sounds.wideMixer) {
                    buttonText = "Use Slim Mode";
                }
                if (GUILayout.Button(new GUIContent(buttonText), EditorStyles.toolbarButton, GUILayout.Width(100))) {
                    UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, buttonText);
                    _sounds.wideMixer = !_sounds.wideMixer;
                }

                GUI.contentColor = Color.white;

                EditorGUILayout.EndHorizontal();
            }

            if (selectAll) {
                UndoHelper.RecordObjectsForUndo(filteredGroups.ToArray(), "Select Groups");

                foreach (var myGroup in filteredGroups) {
                    myGroup.isSelected = true;
                }
            }
            if (deselectAll) {
                UndoHelper.RecordObjectsForUndo(filteredGroups.ToArray(), "Deselect Groups");

                foreach (var myGroup in filteredGroups) {
                    myGroup.isSelected = false;
                }
            }
            if (applyAudioSourceTemplate) {
                UndoHelper.RecordObjectsForUndo(filteredGroups.ToArray(), "Apply Audio Source Template");

                foreach (var myGroup in filteredGroups) {
                    if (!myGroup.isSelected) {
                        continue;
                    }

                    for (var v = 0; v < myGroup.transform.childCount; v++) {
#if UNITY_3_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5
                        Debug.LogError("This feature requires Unity 4 or higher.");
#else
                        var aVar = myGroup.transform.GetChild(v);
                        var oldAudio = aVar.GetComponent<AudioSource>();
                        CopyFromAudioSourceTemplate(oldAudio, true);
#endif
                    }
                }
            }

            if (applyTemplateToAll) {
                UndoHelper.RecordObjectsForUndo(filteredGroups.ToArray(), "Apply Audio Source Template to All");

                foreach (var myGroup in filteredGroups) {
                    for (var v = 0; v < myGroup.transform.childCount; v++) {
#if UNITY_3_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5
                        Debug.LogError("This feature requires Unity 4 or higher.");
#else
                        var aVar = myGroup.transform.GetChild(v);
                        var oldAudio = aVar.GetComponent<AudioSource>();
                        CopyFromAudioSourceTemplate(oldAudio, true);
#endif
                    }
                }
            }
            EditorGUILayout.EndVertical();
            // Sound Groups End

            // Buses
            if (_sounds.groupBuses.Count > 0) {
                DTGUIHelper.VerticalSpace(3);

                var voiceLimitedBuses = _sounds.groupBuses.FindAll(delegate(GroupBus obj) {
                    return obj.voiceLimit >= 0;
                });

                EditorGUILayout.BeginHorizontal();
                GUILayout.Label("Bus Control", GUILayout.Width(100));
                if (voiceLimitedBuses.Count > 0) {
                    GUILayout.FlexibleSpace();
                    GUILayout.Label("Stop Oldest", GUILayout.Width(100));
                    GUILayout.Space(_sounds.wideMixer ? 480 : 284);
                }
                EditorGUILayout.EndHorizontal();

                GroupBus aBus = null;
                var busButtonPressed = DTGUIHelper.DTFunctionButtons.None;
                int? busToDelete = null;
                int? busToSolo = null;
                int? busToMute = null;
                int? busToStop = null;

                for (var i = 0; i < _sounds.groupBuses.Count; i++) {
                    aBus = _sounds.groupBuses[i];

                    DTGUIHelper.StartGroupHeader(1);

                    if (_sounds.ShouldShowUnityAudioMixerGroupAssignments) {
                        EditorGUILayout.BeginVertical();
                        EditorGUILayout.BeginHorizontal();
                    } else {
                        EditorGUILayout.BeginHorizontal();
                    }

                    GUI.color = Color.gray;

                    if (Application.isPlaying) {
                        GUI.color = DTGUIHelper.BrightTextColor;
                        if (aBus.BusVoiceLimitReached) {
                            GUI.contentColor = Color.red;
                            GUI.backgroundColor = Color.red;
                        }
                        if (GUILayout.Button(string.Format("[{0:D2}]", aBus.ActiveVoices), EditorStyles.toolbarButton)) {
                            SelectActiveVariationsInBus(aBus);
                        }
                        GUI.color = Color.white;
                        GUI.contentColor = Color.white;
                        GUI.backgroundColor = Color.white;
                    }

                    GUI.color = Color.white;
                    var newBusName = EditorGUILayout.TextField("", aBus.busName, GUILayout.MaxWidth(170));
                    if (newBusName != aBus.busName) {
                        UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Bus Name");
                        aBus.busName = newBusName;
                    }

                    GUILayout.FlexibleSpace();

                    if (voiceLimitedBuses.Contains(aBus)) {
                        GUI.color = DTGUIHelper.BrightButtonColor;
                        var newMono = GUILayout.Toggle(aBus.stopOldest, new GUIContent("", "Checking this box will make it so when the voice limit is already reached and you play a sound, the oldest sound will first be stopped."));
                        if (newMono != aBus.stopOldest) {
                            UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Stop Oldest");
                            aBus.stopOldest = newMono;
                        }
                    }

                    GUI.color = Color.white;
                    GUILayout.Label("Voices");

                    GUI.color = DTGUIHelper.BrightButtonColor;
                    var oldLimitIndex = busVoiceLimitList.IndexOf(aBus.voiceLimit.ToString());
                    if (oldLimitIndex == -1) {
                        oldLimitIndex = 0;
                    }
                    var busVoiceLimitIndex = EditorGUILayout.Popup("", oldLimitIndex, busVoiceLimitList.ToArray(), GUILayout.MaxWidth(70));
                    if (busVoiceLimitIndex != oldLimitIndex) {
                        UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Bus Voice Limit");
                        aBus.voiceLimit = busVoiceLimitIndex <= 0 ? -1 : busVoiceLimitIndex;
                    }

                    GUI.color = Color.white;

                    EditorGUILayout.BeginHorizontal(GUILayout.MaxWidth(50));
                    GUI.color = DTGUIHelper.BrightTextColor;
                    GUILayout.TextField(DTGUIHelper.DisplayVolumeNumber(aBus.volume, sliderIndicatorChars), sliderIndicatorChars, EditorStyles.miniLabel, GUILayout.Width(sliderWidth));

                    GUI.color = Color.white;
                    var newBusVol = DTGUIHelper.DisplayVolumeField(aBus.volume, DTGUIHelper.VolumeFieldType.Bus, _sounds.wideMixer);
                    if (newBusVol != aBus.volume) {
                        UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Bus Volume");
                        aBus.volume = newBusVol;
                        if (Application.isPlaying) {
                            MasterAudio.SetBusVolumeByName(aBus.busName, aBus.volume);
                        }
                    }

                    GUI.contentColor = Color.white;

                    busButtonPressed = DTGUIHelper.AddMixerBusButtons(aBus);

                    switch (busButtonPressed) {
                        case DTGUIHelper.DTFunctionButtons.Remove:
                            busToDelete = i;
                            break;
                        case DTGUIHelper.DTFunctionButtons.Solo:
                            busToSolo = i;
                            break;
                        case DTGUIHelper.DTFunctionButtons.Mute:
                            busToMute = i;
                            break;
                        case DTGUIHelper.DTFunctionButtons.Stop:
                            busToStop = i;
                            break;
                    }

                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndHorizontal();

#if UNITY_5_0
                    if (!_sounds.ShouldShowUnityAudioMixerGroupAssignments) {
                        DTGUIHelper.EndGroupHeader();
                        continue;
                    }
                    var newChan = (AudioMixerGroup)EditorGUILayout.ObjectField(aBus.mixerChannel, typeof(AudioMixerGroup), false);
                    if (newChan != aBus.mixerChannel) {
                        UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Bus Mixer Group");
                        aBus.mixerChannel = newChan;
                        _sounds.RouteBusToUnityMixerGroup(aBus.busName, newChan);
                    }
                    EditorGUILayout.EndVertical();
#endif
                    DTGUIHelper.EndGroupHeader();
                    DTGUIHelper.AddSpaceForNonU5(2);
                }

                if (busToDelete.HasValue) {
                    DeleteBus(busToDelete.Value);
                }
                if (busToMute.HasValue) {
                    MuteBus(busToMute.Value);
                }
                if (busToSolo.HasValue) {
                    SoloBus(busToSolo.Value);
                }
                if (busToStop.HasValue) {
                    MasterAudio.StopBus(_sounds.groupBuses[busToStop.Value].busName);
                }

                if (Application.isPlaying) {
                    EditorGUILayout.BeginHorizontal();
                    GUILayout.Space(9);
                    GUI.color = DTGUIHelper.BrightTextColor;
                    EditorGUILayout.LabelField(string.Format("[{0:D2}] Total Active Voices", totalVoiceCount));
                    GUI.color = Color.white;
                    EditorGUILayout.EndHorizontal();
                }

                EditorGUILayout.Separator();
                EditorGUILayout.BeginHorizontal();
                GUILayout.Space(10);
                GUI.contentColor = DTGUIHelper.BrightButtonColor;
                GUI.backgroundColor = Color.white;

                if (GUILayout.Button(new GUIContent("Mute/Solo Reset", "Turn off all bus mute and solo switches"), EditorStyles.toolbarButton, GUILayout.Width(120))) {
                    BusMuteSoloReset();
                }

                GUILayout.Space(6);

                if (GUILayout.Button(new GUIContent("Max Bus Volumes", "Reset all bus volumes to full"), EditorStyles.toolbarButton, GUILayout.Width(120))) {
                    UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "Max Bus Volumes");

                    foreach (var t in _sounds.groupBuses) {
                        aBus = t;
                        aBus.volume = 1f;
                    }
                }

#if UNITY_5_0
                GUILayout.Space(6);

                GUI.contentColor = DTGUIHelper.BrightButtonColor;
                var buttonText = "Show Unity Mixer Groups";
                if (_sounds.showUnityMixerGroupAssignment) {
                    buttonText = "Hide Unity Mixer Groups";
                }
                if (GUILayout.Button(new GUIContent(buttonText), EditorStyles.toolbarButton, GUILayout.Width(140))) {
                    UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, buttonText);
                    _sounds.showUnityMixerGroupAssignment = !_sounds.showUnityMixerGroupAssignment;
                }
#endif

                GUI.contentColor = Color.white;
                EditorGUILayout.EndHorizontal();
            }

            DTGUIHelper.EndGroupedControls();
        }
        // Sound Buses End

        // Music playlist Start		
        DTGUIHelper.VerticalSpace(3);
        EditorGUILayout.BeginHorizontal();
        EditorGUI.indentLevel = 0;  // Space will handle this for the header

        GUI.color = Color.white;
        GUI.contentColor = Color.white;
        GUI.backgroundColor = Color.white;


        state = _sounds.playListExpanded;
        text = "Playlist Settings";

        // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
        if (!state) {
            GUI.backgroundColor = DTGUIHelper.InactiveHeaderColor;
        } else {
            GUI.backgroundColor = DTGUIHelper.ActiveHeaderColor;
        }

        GUILayout.BeginHorizontal();

#if UNITY_3_5_7
        if (!state) {
            text += " (Click to expand)";
        }
#else
        text = "<b><size=11>" + text + "</size></b>";
#endif
        if (state) {
            text = "\u25BC " + text;
        } else {
            text = "\u25BA " + text;
        }
        if (!GUILayout.Toggle(true, text, "dragtab", GUILayout.MinWidth(20f))) {
            state = !state;
        }

        GUILayout.Space(2f);

        var isExp = state;


        if (isExp != _sounds.playListExpanded) {
            UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Playlist Settings");
            _sounds.playListExpanded = isExp;
        }

        EditorGUILayout.EndHorizontal();
        GUI.color = Color.white;

        EditorGUILayout.EndHorizontal();


        if (_sounds.playListExpanded) {
            DTGUIHelper.BeginGroupedControls();

#if UNITY_5_0
            var newMusicSpatialType = (MasterAudio.AllMusicSpatialBlendType)EditorGUILayout.EnumPopup("Music Spatial Blend Rule", _sounds.musicSpatialBlendType);
            if (newMusicSpatialType != _sounds.musicSpatialBlendType) {
                UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Music Spatial Blend Rule");
                _sounds.musicSpatialBlendType = newMusicSpatialType;

                if (Application.isPlaying) {
                    SetSpatialBlendsForPlaylistControllers();
                }
            }

            switch (_sounds.musicSpatialBlendType) {
                case MasterAudio.AllMusicSpatialBlendType.ForceAllToCustom:
                    EditorGUI.indentLevel = 1;
                    DTGUIHelper.ShowLargeBarAlert(SpatialBlendSliderText);
                    var newMusic3D = EditorGUILayout.Slider("Music Spatial Blend", _sounds.musicSpatialBlend, 0f, 1f);
                    if (newMusic3D != _sounds.musicSpatialBlend) {
                        UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Music Spatial Blend");
                        _sounds.musicSpatialBlend = newMusic3D;
                        if (Application.isPlaying) {
                            SetSpatialBlendsForPlaylistControllers();
                        }
                    }
                    break;
                case MasterAudio.AllMusicSpatialBlendType.AllowDifferentPerController:
                    DTGUIHelper.ShowLargeBarAlert("To set Spatial Blend, go to each Playlist Controller and change it there.");
                    break;
            }

            EditorGUILayout.Separator();
#endif

            EditorGUI.indentLevel = 0;
            DTGUIHelper.StartGroupHeader();
            EditorGUILayout.BeginHorizontal();
            EditorGUILayout.LabelField("Playlist Controller Setup", EditorStyles.miniBoldLabel, GUILayout.Width(146));
            if (plControllerInScene) {
                GUILayout.FlexibleSpace();
                EditorGUILayout.LabelField("Sync Grp.", EditorStyles.miniBoldLabel, GUILayout.Width(54));
                EditorGUILayout.LabelField("Initial Playlist", EditorStyles.miniBoldLabel, GUILayout.Width(100));
                GUILayout.Space(Application.isPlaying ? 188 - extraPlaylistLength : 214 - extraPlaylistLength);
            }
            EditorGUILayout.EndHorizontal();

            if (!plControllerInScene) {
                DTGUIHelper.ShowLargeBarAlert("There are no Playlist Controllers in the scene. Music will not play.");
            } else {
                int? indexToDelete = null;

                _playlistNames.Insert(0, MasterAudio.NoPlaylistName);

                var syncGroupList = new List<string>();
                for (var i = 0; i < 4; i++) {
                    syncGroupList.Add((i + 1).ToString());
                }
                syncGroupList.Insert(0, MasterAudio.NoGroupName);

                for (var i = 0; i < pcs.Count; i++) {
                    var controller = pcs[i];
                    DTGUIHelper.StartGroupHeader(1);

#if UNITY_5_0
                    EditorGUILayout.BeginVertical();
                    EditorGUILayout.BeginHorizontal();
#else
                    EditorGUILayout.BeginHorizontal();
#endif

                    GUILayout.Label(controller.name);

                    GUILayout.FlexibleSpace();

                    var ctrlDirty = false;

                    GUI.color = DTGUIHelper.BrightButtonColor;
                    var syncIndex = syncGroupList.IndexOf(controller.syncGroupNum.ToString());
                    if (syncIndex == -1) {
                        syncIndex = 0;
                    }
                    var newSync = EditorGUILayout.Popup("", syncIndex, syncGroupList.ToArray(), GUILayout.Width(55));
                    if (newSync != syncIndex) {
                        UndoHelper.RecordObjectPropertyForUndo(ref ctrlDirty, controller, "change Controller Sync Group");
                        controller.syncGroupNum = newSync;
                    }

                    var origIndex = _playlistNames.IndexOf(controller.startPlaylistName);
                    if (origIndex == -1) {
                        origIndex = 0;
                        controller.startPlaylistName = string.Empty;
                    }
                    var newIndex = EditorGUILayout.Popup("", origIndex, _playlistNames.ToArray(), GUILayout.Width(playlistListWidth));
                    if (newIndex != origIndex) {
                        UndoHelper.RecordObjectPropertyForUndo(ref ctrlDirty, controller, "change Playlist Controller initial Playlist");
                        controller.startPlaylistName = _playlistNames[newIndex];
                    }
                    GUI.color = Color.white;

                    GUI.contentColor = DTGUIHelper.BrightButtonColor;
                    GUILayout.TextField(DTGUIHelper.DisplayVolumeNumber(controller._playlistVolume, sliderIndicatorChars), sliderIndicatorChars, EditorStyles.miniLabel, GUILayout.Width(sliderWidth));
                    var newVol = DTGUIHelper.DisplayVolumeField(controller._playlistVolume, DTGUIHelper.VolumeFieldType.PlaylistController, false);

                    if (newVol != controller._playlistVolume) {
                        UndoHelper.RecordObjectPropertyForUndo(ref ctrlDirty, controller, "change Playlist Controller volume");
                        controller.PlaylistVolume = newVol;
                    }

                    GUI.contentColor = Color.white;

                    var buttonPressed = DTGUIHelper.AddPlaylistControllerSetupButtons(controller, "Playlist Controller", false);

                    EditorGUILayout.EndHorizontal();

#if UNITY_5_0
                    if (_sounds.showUnityMixerGroupAssignment) {
                        var newChan = (AudioMixerGroup)EditorGUILayout.ObjectField(controller.mixerChannel, typeof(AudioMixerGroup), false);
                        if (newChan != controller.mixerChannel) {
                            UndoHelper.RecordObjectPropertyForUndo(ref ctrlDirty, controller, "change Playlist Controller Unity Mixer Group");
                            controller.mixerChannel = newChan;

                            if (Application.isPlaying) {
                                controller.RouteToMixerChannel(newChan);
                            }
                        }
                    }

                    EditorGUILayout.EndVertical();
#endif

                    switch (buttonPressed) {
                        case DTGUIHelper.DTFunctionButtons.Go:
                            Selection.activeObject = controller.transform;
                            break;
                        case DTGUIHelper.DTFunctionButtons.Remove:
                            indexToDelete = i;
                            break;
                        case DTGUIHelper.DTFunctionButtons.Mute:
                            controller.ToggleMutePlaylist();
                            break;
                    }

                    DTGUIHelper.EndGroupHeader();
                    DTGUIHelper.AddSpaceForNonU5(1);

                    if (ctrlDirty) {
                        EditorUtility.SetDirty(controller);
                    }
                }

                if (indexToDelete.HasValue) {
                    UndoHelper.DestroyForUndo(pcs[indexToDelete.Value].gameObject);
                }
            }

            EditorGUILayout.Separator();
            GUI.contentColor = DTGUIHelper.BrightButtonColor;
            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(10);
            GUI.backgroundColor = Color.white;
            if (GUILayout.Button(new GUIContent("Create Playlist Controller"), EditorStyles.toolbarButton, GUILayout.Width(150))) {
                // ReSharper disable once RedundantCast
                var go = (GameObject)Instantiate(_sounds.playlistControllerPrefab.gameObject);
                go.name = "PlaylistController";

                UndoHelper.CreateObjectForUndo(go, "create Playlist Controller");
            }

            var buttonText = string.Empty;

#if UNITY_5_0
            GUILayout.Space(6);
            GUI.contentColor = DTGUIHelper.BrightButtonColor;
            buttonText = "Show Unity Mixer Groups";
            if (_sounds.showUnityMixerGroupAssignment) {
                buttonText = "Hide Unity Mixer Groups";
            }
            if (GUILayout.Button(new GUIContent(buttonText), EditorStyles.toolbarButton, GUILayout.Width(140))) {
                UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, buttonText);
                _sounds.showUnityMixerGroupAssignment = !_sounds.showUnityMixerGroupAssignment;
            }
#endif

            EditorGUILayout.EndHorizontal();
            DTGUIHelper.EndGroupHeader();

            GUI.contentColor = Color.white;

            EditorGUILayout.Separator();

            EditorGUILayout.LabelField("Playlist Setup", EditorStyles.miniBoldLabel);
            EditorGUI.indentLevel = 0;  // Space will handle this for the header

            if (_sounds.musicPlaylists.Count == 0) {
                DTGUIHelper.ShowLargeBarAlert("You currently have no Playlists set up.");
            }

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbar);
            EditorGUI.indentLevel = 1;
            var oldPlayExpanded = DTGUIHelper.Foldout(_sounds.playlistsExpanded, string.Format("Playlists ({0})", _sounds.musicPlaylists.Count));
            if (oldPlayExpanded != _sounds.playlistsExpanded) {
                UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Playlists");
                _sounds.playlistsExpanded = oldPlayExpanded;
            }

            EditorGUILayout.BeginHorizontal(GUILayout.MaxWidth(100));

            var addPressed = false;

            buttonText = "Click to add new Playlist at the end";
            // Add button - Process presses later
            GUI.contentColor = DTGUIHelper.BrightButtonColor;
            addPressed = GUILayout.Button(new GUIContent("Add", buttonText),
                                               EditorStyles.toolbarButton);
            GUIContent content;
            GUI.contentColor = DTGUIHelper.BrightButtonColor;

            content = new GUIContent("Collapse", "Click to collapse all");
            var masterCollapse = GUILayout.Button(content, EditorStyles.toolbarButton);

            content = new GUIContent("Expand", "Click to expand all");
            var masterExpand = GUILayout.Button(content, EditorStyles.toolbarButton);
            if (masterExpand) {
                ExpandCollapseAllPlaylists(true);
            }
            if (masterCollapse) {
                ExpandCollapseAllPlaylists(false);
            }
            GUI.contentColor = Color.white;
            EditorGUILayout.EndHorizontal();
            EditorGUILayout.EndHorizontal();

            if (_sounds.playlistsExpanded) {
                int? playlistToRemove = null;
                int? playlistToInsertAt = null;
                int? playlistToMoveUp = null;
                int? playlistToMoveDown = null;

                for (var i = 0; i < _sounds.musicPlaylists.Count; i++) {
                    var aList = _sounds.musicPlaylists[i];

                    DTGUIHelper.StartGroupHeader();

                    EditorGUI.indentLevel = 1;
                    EditorGUILayout.BeginHorizontal();
                    aList.isExpanded = DTGUIHelper.Foldout(aList.isExpanded, "Playlist: " + aList.playlistName);

                    var playlistButtonPressed = DTGUIHelper.AddFoldOutListItemButtons(i, _sounds.musicPlaylists.Count, "playlist", false, true);

                    EditorGUILayout.EndHorizontal();
                    EditorGUILayout.EndVertical();

                    if (aList.isExpanded) {
                        EditorGUI.indentLevel = 0;
                        var newPlaylist = EditorGUILayout.TextField("Name", aList.playlistName);
                        if (newPlaylist != aList.playlistName) {
                            UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Name");
                            aList.playlistName = newPlaylist;
                        }

                        var crossfadeMode = (MasterAudio.Playlist.CrossfadeTimeMode)EditorGUILayout.EnumPopup("Crossfade Mode", aList.crossfadeMode);
                        if (crossfadeMode != aList.crossfadeMode) {
                            UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Crossfade Mode");
                            aList.crossfadeMode = crossfadeMode;
                        }
                        if (aList.crossfadeMode == MasterAudio.Playlist.CrossfadeTimeMode.Override) {
                            var newCf = EditorGUILayout.Slider("Crossfade time (sec)", aList.crossFadeTime, 0f, 10f);
                            if (newCf != aList.crossFadeTime) {
                                UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Crossfade time (sec)");
                                aList.crossFadeTime = newCf;
                            }
                        }

                        var newFadeIn = EditorGUILayout.Toggle("Fade In First Song", aList.fadeInFirstSong);
                        if (newFadeIn != aList.fadeInFirstSong) {
                            UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Fade In First Song");
                            aList.fadeInFirstSong = newFadeIn;
                        }

                        var newFadeOut = EditorGUILayout.Toggle("Fade Out Last Song", aList.fadeOutLastSong);
                        if (newFadeOut != aList.fadeOutLastSong) {
                            UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Fade Out Last Song");
                            aList.fadeOutLastSong = newFadeOut;
                        }

                        var newTransType = (MasterAudio.SongFadeInPosition)EditorGUILayout.EnumPopup("Song Transition Type", aList.songTransitionType);
                        if (newTransType != aList.songTransitionType) {
                            UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Song Transition Type");
                            aList.songTransitionType = newTransType;
                        }
                        if (aList.songTransitionType == MasterAudio.SongFadeInPosition.SynchronizeClips) {
                            DTGUIHelper.ShowColorWarning("*All clips must be of exactly the same length in this mode.");
                        }

                        EditorGUI.indentLevel = 0;
                        var newBulkMode = (MasterAudio.AudioLocation)EditorGUILayout.EnumPopup("Clip Create Mode", aList.bulkLocationMode);
                        if (newBulkMode != aList.bulkLocationMode) {
                            UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Bulk Clip Mode");
                            aList.bulkLocationMode = newBulkMode;
                        }

                        var playlistHasResource = false;
                        foreach (var t in aList.MusicSettings) {
                            if (t.audLocation != MasterAudio.AudioLocation.ResourceFile) {
                                continue;
                            }

                            playlistHasResource = true;
                            break;
                        }

                        if (MasterAudio.HasAsyncResourceLoaderFeature() && playlistHasResource) {
                            if (!_sounds.resourceClipsAllLoadAsync) {
                                var newAsync = EditorGUILayout.Toggle(new GUIContent("Load Resources Async", "Checking this means Resource files in this Playlist will be loaded asynchronously."), aList.resourceClipsAllLoadAsync);
                                if (newAsync != aList.resourceClipsAllLoadAsync) {
                                    UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Load Resources Async");
                                    aList.resourceClipsAllLoadAsync = newAsync;
                                }
                            }
                        }

                        EditorGUILayout.BeginHorizontal();
                        GUILayout.Space(10);
                        GUI.contentColor = DTGUIHelper.BrightButtonColor;
                        if (GUILayout.Button(new GUIContent("Eq. Song Volumes"), EditorStyles.toolbarButton, GUILayout.Width(110))) {
                            EqualizePlaylistVolumes(aList.MusicSettings);
                        }

                        var hasExpanded = false;
                        foreach (var t in aList.MusicSettings) {
                            if (t.isExpanded) {
                                continue;
                            }

                            hasExpanded = true;
                            break;
                        }

                        var theButtonText = hasExpanded ? "Collapse All" : "Expand All";

                        GUILayout.Space(10);
                        GUI.contentColor = DTGUIHelper.BrightButtonColor;
                        if (GUILayout.Button(new GUIContent(theButtonText), EditorStyles.toolbarButton, GUILayout.Width(100))) {
                            ExpandCollapseSongs(aList, !hasExpanded);
                        }
                        GUILayout.Space(10);
                        if (GUILayout.Button(new GUIContent("Sort Alpha"), EditorStyles.toolbarButton, GUILayout.Width(100))) {
                            SortSongsAlpha(aList);
                        }

                        GUILayout.FlexibleSpace();
                        EditorGUILayout.EndHorizontal();
                        GUI.contentColor = Color.white;
                        EditorGUILayout.Separator();

                        EditorGUILayout.BeginVertical();
                        var anEvent = Event.current;

                        GUI.color = DTGUIHelper.DragAreaColor;

                        var dragArea = GUILayoutUtility.GetRect(0f, 35f, GUILayout.ExpandWidth(true));
                        GUI.Box(dragArea, "Drag Audio clips here to add to playlist!");

                        GUI.color = Color.white;

                        switch (anEvent.type) {
                            case EventType.DragUpdated:
                            case EventType.DragPerform:
                                if (!dragArea.Contains(anEvent.mousePosition)) {
                                    break;
                                }

                                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                                if (anEvent.type == EventType.DragPerform) {
                                    DragAndDrop.AcceptDrag();

                                    foreach (var dragged in DragAndDrop.objectReferences) {
                                        var aClip = dragged as AudioClip;
                                        if (aClip == null) {
                                            continue;
                                        }

                                        AddSongToPlaylist(aList, aClip);
                                    }
                                }
                                Event.current.Use();
                                break;
                        }
                        EditorGUILayout.EndVertical();

                        EditorGUI.indentLevel = 2;

                        int? addIndex = null;
                        int? removeIndex = null;
                        int? moveUpIndex = null;
                        int? moveDownIndex = null;

                        if (aList.MusicSettings.Count == 0) {
                            EditorGUI.indentLevel = 0;
                            DTGUIHelper.ShowLargeBarAlert("You currently have no songs in this Playlist.");
                        }

                        EditorGUI.indentLevel = 0;
                        for (var j = 0; j < aList.MusicSettings.Count; j++) {
                            DTGUIHelper.StartGroupHeader(1);

                            var aSong = aList.MusicSettings[j];
                            var clipName = "Empty";
                            switch (aSong.audLocation) {
                                case MasterAudio.AudioLocation.Clip:
                                    if (aSong.clip != null) {
                                        clipName = aSong.clip.name;
                                    }
                                    break;
                                case MasterAudio.AudioLocation.ResourceFile:
                                    if (!string.IsNullOrEmpty(aSong.resourceFileName)) {
                                        clipName = aSong.resourceFileName;
                                    }
                                    break;
                            }
                            EditorGUILayout.BeginHorizontal();
                            EditorGUI.indentLevel = 1;

                            if (!string.IsNullOrEmpty(clipName) && string.IsNullOrEmpty(aSong.songName)) {
                                switch (aSong.audLocation) {
                                    case MasterAudio.AudioLocation.Clip:
                                        aSong.songName = clipName;
                                        break;
                                    case MasterAudio.AudioLocation.ResourceFile:
                                        aSong.songName = clipName;
                                        break;
                                }
                            }

                            var newSongExpanded = DTGUIHelper.Foldout(aSong.isExpanded, aSong.songName);
                            if (newSongExpanded != aSong.isExpanded) {
                                UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Song expand");
                                aSong.isExpanded = newSongExpanded;
                            }
                            var songButtonPressed = DTGUIHelper.AddFoldOutListItemButtons(j, aList.MusicSettings.Count, "clip", false, true, true);
                            EditorGUILayout.EndHorizontal();
                            EditorGUILayout.EndVertical();

                            if (aSong.isExpanded) {
                                EditorGUI.indentLevel = 0;

                                var oldLocation = aSong.audLocation;
                                var newClipSource = (MasterAudio.AudioLocation)EditorGUILayout.EnumPopup("Audio Origin", aSong.audLocation);
                                if (newClipSource != aSong.audLocation) {
                                    UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Audio Origin");
                                    aSong.audLocation = newClipSource;
                                }

                                switch (aSong.audLocation) {
                                    case MasterAudio.AudioLocation.Clip:
                                        var newClip = (AudioClip)EditorGUILayout.ObjectField("Audio Clip", aSong.clip, typeof(AudioClip), true);
                                        if (newClip != aSong.clip) {
                                            UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Clip");
                                            aSong.clip = newClip;
                                            var cName = newClip == null ? "Empty" : newClip.name;
                                            aSong.songName = cName;
                                        }
                                        break;
                                    case MasterAudio.AudioLocation.ResourceFile:
                                        if (oldLocation != aSong.audLocation) {
                                            if (aSong.clip != null) {
                                                Debug.Log("Audio clip removed to prevent unnecessary memory usage on Resource file Playlist clip.");
                                            }
                                            aSong.clip = null;
                                            aSong.songName = string.Empty;
                                        }

                                        EditorGUILayout.BeginVertical();
                                        anEvent = Event.current;

                                        GUI.color = DTGUIHelper.DragAreaColor;
                                        dragArea = GUILayoutUtility.GetRect(0f, 20f, GUILayout.ExpandWidth(true));
                                        GUI.Box(dragArea, "Drag Resource Audio clip here to use its name!");
                                        GUI.color = Color.white;

                                        switch (anEvent.type) {
                                            case EventType.DragUpdated:
                                            case EventType.DragPerform:
                                                if (!dragArea.Contains(anEvent.mousePosition)) {
                                                    break;
                                                }

                                                DragAndDrop.visualMode = DragAndDropVisualMode.Copy;

                                                if (anEvent.type == EventType.DragPerform) {
                                                    DragAndDrop.AcceptDrag();

                                                    foreach (var dragged in DragAndDrop.objectReferences) {
                                                        var aClip = dragged as AudioClip;
                                                        if (aClip == null) {
                                                            continue;
                                                        }

                                                        UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Resource Filename");

                                                        var unused = false;
                                                        var resourceFileName = DTGUIHelper.GetResourcePath(aClip, ref unused);
                                                        if (string.IsNullOrEmpty(resourceFileName)) {
                                                            resourceFileName = aClip.name;
                                                        }

                                                        aSong.resourceFileName = resourceFileName;
                                                        aSong.songName = aClip.name;
                                                    }
                                                }
                                                Event.current.Use();
                                                break;
                                        }
                                        EditorGUILayout.EndVertical();

                                        var newFilename = EditorGUILayout.TextField("Resource Filename", aSong.resourceFileName);
                                        if (newFilename != aSong.resourceFileName) {
                                            UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Resource Filename");
                                            aSong.resourceFileName = newFilename;
                                        }

                                        break;
                                }

                                var newVol = DTGUIHelper.DisplayVolumeField(aSong.volume, DTGUIHelper.VolumeFieldType.None, false, 0f, true);
                                if (newVol != aSong.volume) {
                                    UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Volume");
                                    aSong.volume = newVol;
                                }

                                var newPitch = DTGUIHelper.DisplayPitchField(aSong.pitch);
                                if (newPitch != aSong.pitch) {
                                    UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Pitch");
                                    aSong.pitch = newPitch;
                                }

                                if (aList.songTransitionType == MasterAudio.SongFadeInPosition.SynchronizeClips) {
                                    DTGUIHelper.ShowLargeBarAlert("All songs must loop in Synchronized Playlists.");
                                } else {
                                    var newLoop = EditorGUILayout.Toggle("Loop Clip", aSong.isLoop);
                                    if (newLoop != aSong.isLoop) {
                                        UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Loop Clip");
                                        aSong.isLoop = newLoop;
                                    }
                                }

                                if (aList.songTransitionType == MasterAudio.SongFadeInPosition.NewClipFromBeginning) {
                                    var newStart = EditorGUILayout.FloatField("Start Time (seconds)", aSong.customStartTime, GUILayout.Width(300));
                                    if (newStart < 0) {
                                        newStart = 0f;
                                    }
                                    if (newStart != aSong.customStartTime) {
                                        UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Start Time (seconds)");
                                        aSong.customStartTime = newStart;
                                    }
                                }

                                GUI.color = Color.white;
                                var exp = EditorGUILayout.BeginToggleGroup(" Fire 'Song Started' Event", aSong.songStartedEventExpanded);
                                if (exp != aSong.songStartedEventExpanded) {
                                    UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle expand Fire 'Song Started' Event");
                                    aSong.songStartedEventExpanded = exp;
                                }
                                GUI.color = Color.white;

                                if (aSong.songStartedEventExpanded) {
                                    EditorGUI.indentLevel = 1;
                                    DTGUIHelper.ShowColorWarning("*When song starts, fire Custom Event below.");

                                    var existingIndex = _sounds.CustomEventNames.IndexOf(aSong.songStartedCustomEvent);

                                    int? customEventIndex = null;

                                    var noEvent = false;
                                    var noMatch = false;

                                    if (existingIndex >= 1) {
                                        customEventIndex = EditorGUILayout.Popup("Custom Event Name", existingIndex, _sounds.CustomEventNames.ToArray());
                                        if (existingIndex == 1) {
                                            noEvent = true;
                                        }
                                    } else if (existingIndex == -1 && aSong.songStartedCustomEvent == MasterAudio.NoGroupName) {
                                        customEventIndex = EditorGUILayout.Popup("Custom Event Name", existingIndex, _sounds.CustomEventNames.ToArray());
                                    } else { // non-match
                                        noMatch = true;
                                        var newEventName = EditorGUILayout.TextField("Custom Event Name", aSong.songStartedCustomEvent);
                                        if (newEventName != aSong.songStartedCustomEvent) {
                                            UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Custom Event Name");
                                            aSong.songStartedCustomEvent = newEventName;
                                        }

                                        var newIndex = EditorGUILayout.Popup("All Custom Events", -1, _sounds.CustomEventNames.ToArray());
                                        if (newIndex >= 0) {
                                            customEventIndex = newIndex;
                                        }
                                    }

                                    if (noEvent) {
                                        DTGUIHelper.ShowRedError("No Custom Event specified. This section will do nothing.");
                                    } else if (noMatch) {
                                        DTGUIHelper.ShowRedError("Custom Event found no match. Type in or choose one.");
                                    }

                                    if (customEventIndex.HasValue) {
                                        if (existingIndex != customEventIndex.Value) {
                                            UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Custom Event");
                                        }
                                        // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                                        if (customEventIndex.Value == -1) {
                                            aSong.songStartedCustomEvent = MasterAudio.NoGroupName;
                                        } else {
                                            aSong.songStartedCustomEvent = _sounds.CustomEventNames[customEventIndex.Value];
                                        }
                                    }
                                }
                                EditorGUILayout.EndToggleGroup();

                                EditorGUI.indentLevel = 0;

                                if (_sounds.useGaplessPlaylists) {
                                    DTGUIHelper.ShowLargeBarAlert("Song Changed Event cannot be used with gapless transitions.");
                                } else {
                                    exp = EditorGUILayout.BeginToggleGroup(" Fire 'Song Changed' Event", aSong.songChangedEventExpanded);
                                    if (exp != aSong.songChangedEventExpanded) {
                                        UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle expand Fire 'Song Changed' Event");
                                        aSong.songChangedEventExpanded = exp;
                                    }
                                    GUI.color = Color.white;

                                    if (aSong.songChangedEventExpanded) {
                                        EditorGUI.indentLevel = 1;
                                        DTGUIHelper.ShowColorWarning("*When song changes to another, fire Custom Event below.");

                                        var existingIndex = _sounds.CustomEventNames.IndexOf(aSong.songChangedCustomEvent);

                                        int? customEventIndex = null;

                                        var noEvent = false;
                                        var noMatch = false;

                                        if (existingIndex >= 1) {
                                            customEventIndex = EditorGUILayout.Popup("Custom Event Name", existingIndex, _sounds.CustomEventNames.ToArray());
                                            if (existingIndex == 1) {
                                                noEvent = true;
                                            }
                                        } else if (existingIndex == -1 && aSong.songChangedCustomEvent == MasterAudio.NoGroupName) {
                                            customEventIndex = EditorGUILayout.Popup("Custom Event Name", existingIndex, _sounds.CustomEventNames.ToArray());
                                        } else { // non-match
                                            noMatch = true;
                                            var newEventName = EditorGUILayout.TextField("Custom Event Name", aSong.songChangedCustomEvent);
                                            if (newEventName != aSong.songChangedCustomEvent) {
                                                UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Custom Event Name");
                                                aSong.songChangedCustomEvent = newEventName;
                                            }

                                            var newIndex = EditorGUILayout.Popup("All Custom Events", -1, _sounds.CustomEventNames.ToArray());
                                            if (newIndex >= 0) {
                                                customEventIndex = newIndex;
                                            }
                                        }

                                        if (noEvent) {
                                            DTGUIHelper.ShowRedError("No Custom Event specified. This section will do nothing.");
                                        } else if (noMatch) {
                                            DTGUIHelper.ShowRedError("Custom Event found no match. Type in or choose one.");
                                        }

                                        if (customEventIndex.HasValue) {
                                            if (existingIndex != customEventIndex.Value) {
                                                UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Custom Event");
                                            }
                                            // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
                                            if (customEventIndex.Value == -1) {
                                                aSong.songChangedCustomEvent = MasterAudio.NoGroupName;
                                            } else {
                                                aSong.songChangedCustomEvent = _sounds.CustomEventNames[customEventIndex.Value];
                                            }
                                        }
                                    }
                                    EditorGUILayout.EndToggleGroup();
                                }
                            }

                            switch (songButtonPressed) {
                                case DTGUIHelper.DTFunctionButtons.Add:
                                    addIndex = j;
                                    break;
                                case DTGUIHelper.DTFunctionButtons.Remove:
                                    removeIndex = j;
                                    break;
                                case DTGUIHelper.DTFunctionButtons.ShiftUp:
                                    moveUpIndex = j;
                                    break;
                                case DTGUIHelper.DTFunctionButtons.ShiftDown:
                                    moveDownIndex = j;
                                    break;
                                case DTGUIHelper.DTFunctionButtons.Play:
                                    StopPreviewer();
                                    switch (aSong.audLocation) {
                                        case MasterAudio.AudioLocation.Clip:
                                            GetPreviewer().PlayOneShot(aSong.clip, aSong.volume);
                                            break;
                                        case MasterAudio.AudioLocation.ResourceFile:
                                            GetPreviewer().PlayOneShot(Resources.Load(aSong.resourceFileName) as AudioClip, aSong.volume);
                                            break;
                                    }
                                    break;
                                case DTGUIHelper.DTFunctionButtons.Stop:
                                    GetPreviewer().clip = null;
                                    StopPreviewer();
                                    break;
                            }
                            EditorGUILayout.EndVertical();
                            DTGUIHelper.AddSpaceForNonU5(2);
                        }

                        if (addIndex.HasValue) {
                            var mus = new MusicSetting();
                            UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "add song");
                            aList.MusicSettings.Insert(addIndex.Value + 1, mus);
                        } else if (removeIndex.HasValue) {
                            UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "delete song");
                            aList.MusicSettings.RemoveAt(removeIndex.Value);
                        } else if (moveUpIndex.HasValue) {
                            var item = aList.MusicSettings[moveUpIndex.Value];

                            UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "shift up song");

                            aList.MusicSettings.Insert(moveUpIndex.Value - 1, item);
                            aList.MusicSettings.RemoveAt(moveUpIndex.Value + 1);
                        } else if (moveDownIndex.HasValue) {
                            var index = moveDownIndex.Value + 1;
                            var item = aList.MusicSettings[index];

                            UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "shift down song");

                            aList.MusicSettings.Insert(index - 1, item);
                            aList.MusicSettings.RemoveAt(index + 1);
                        }
                    }

                    switch (playlistButtonPressed) {
                        case DTGUIHelper.DTFunctionButtons.Remove:
                            playlistToRemove = i;
                            break;
                        case DTGUIHelper.DTFunctionButtons.Add:
                            playlistToInsertAt = i;
                            break;
                        case DTGUIHelper.DTFunctionButtons.ShiftUp:
                            playlistToMoveUp = i;
                            break;
                        case DTGUIHelper.DTFunctionButtons.ShiftDown:
                            playlistToMoveDown = i;
                            break;
                    }

                    EditorGUILayout.EndVertical();
                    DTGUIHelper.AddSpaceForNonU5(4);
                }

                if (playlistToRemove.HasValue) {
                    UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "delete Playlist");
                    _sounds.musicPlaylists.RemoveAt(playlistToRemove.Value);
                }
                if (playlistToInsertAt.HasValue) {
                    var pl = new MasterAudio.Playlist();
                    UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "add Playlist");
                    _sounds.musicPlaylists.Insert(playlistToInsertAt.Value + 1, pl);
                }
                if (playlistToMoveUp.HasValue) {
                    var item = _sounds.musicPlaylists[playlistToMoveUp.Value];
                    UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "shift up Playlist");
                    _sounds.musicPlaylists.Insert(playlistToMoveUp.Value - 1, item);
                    _sounds.musicPlaylists.RemoveAt(playlistToMoveUp.Value + 1);
                }
                if (playlistToMoveDown.HasValue) {
                    var index = playlistToMoveDown.Value + 1;
                    var item = _sounds.musicPlaylists[index];

                    UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "shift down Playlist");

                    _sounds.musicPlaylists.Insert(index - 1, item);
                    _sounds.musicPlaylists.RemoveAt(index + 1);
                }
            }

            if (addPressed) {
                UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "add Playlist");
                _sounds.musicPlaylists.Add(new MasterAudio.Playlist());
            }

            DTGUIHelper.EndGroupedControls();
        }
        // Music playlist End

        // Custom Events Start
        EditorGUI.indentLevel = 0;
        DTGUIHelper.VerticalSpace(3);

        GUI.color = Color.white;
        GUI.backgroundColor = Color.white;
        GUI.contentColor = Color.white;

        state = _sounds.showCustomEvents;
        text = "Custom Events";

        // ReSharper disable once ConvertIfStatementToConditionalTernaryExpression
        if (!state) {
            GUI.backgroundColor = DTGUIHelper.InactiveHeaderColor;
        } else {
            GUI.backgroundColor = DTGUIHelper.ActiveHeaderColor;
        }

        GUILayout.BeginHorizontal();

#if UNITY_3_5_7
        if (!state) {
            text += " (Click to expand)";
        }
#else
        text = "<b><size=11>" + text + "</size></b>";
#endif
        if (state) {
            text = "\u25BC " + text;
        } else {
            text = "\u25BA " + text;
        }
        if (!GUILayout.Toggle(true, text, "dragtab", GUILayout.MinWidth(20f))) {
            state = !state;
        }

        GUILayout.Space(2f);

        isExp = state;

        if (isExp != _sounds.showCustomEvents) {
            UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Custom Events");
            _sounds.showCustomEvents = isExp;
        }
        EditorGUILayout.EndHorizontal();
        GUI.color = Color.white;

        if (_sounds.showCustomEvents) {
            DTGUIHelper.BeginGroupedControls();
            var newEvent = EditorGUILayout.TextField("New Event Name", _sounds.newEventName);
            if (newEvent != _sounds.newEventName) {
                UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change New Event Name");
                _sounds.newEventName = newEvent;
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Space(10);
            GUI.contentColor = DTGUIHelper.BrightButtonColor;
            if (GUILayout.Button("Create New Event", EditorStyles.toolbarButton, GUILayout.Width(100))) {
                CreateCustomEvent(_sounds.newEventName);
            }
            GUILayout.Space(10);
            GUI.contentColor = DTGUIHelper.BrightButtonColor;

            var hasExpanded = false;
            foreach (var t in _sounds.customEvents) {
                if (t.eventExpanded) {
                    continue;
                }
                hasExpanded = true;
                break;
            }

            var buttonText = hasExpanded ? "Collapse All" : "Expand All";

            if (GUILayout.Button(buttonText, EditorStyles.toolbarButton, GUILayout.Width(100))) {
                ExpandCollapseCustomEvents(!hasExpanded);
            }
            GUILayout.Space(10);
            if (GUILayout.Button("Sort Alpha", EditorStyles.toolbarButton, GUILayout.Width(100))) {
                SortCustomEvents();
            }

            GUI.contentColor = Color.white;
            EditorGUILayout.EndHorizontal();

            if (_sounds.customEvents.Count == 0) {
                DTGUIHelper.ShowLargeBarAlert("You currently have no Custom Events.");
            }

            int? customEventToDelete = null;
            int? eventToRename = null;

            for (var i = 0; i < _sounds.customEvents.Count; i++) {
                EditorGUI.indentLevel = 1;
                var anEvent = _sounds.customEvents[i];

                DTGUIHelper.AddSpaceForNonU5(2);
                DTGUIHelper.StartGroupHeader();

                EditorGUILayout.BeginHorizontal();
                var exp = DTGUIHelper.Foldout(anEvent.eventExpanded, anEvent.EventName);
                if (exp != anEvent.eventExpanded) {
                    UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle expand Custom Event");
                    anEvent.eventExpanded = exp;
                }

                GUILayout.FlexibleSpace();
                if (Application.isPlaying) {
                    var receivers = MasterAudio.ReceiversForEvent(anEvent.EventName);

                    GUI.contentColor = DTGUIHelper.BrightButtonColor;
                    if (receivers.Count > 0) {
                        if (GUILayout.Button(new GUIContent("Select", "Click this button to select all Receivers in the Hierarchy"), EditorStyles.toolbarButton, GUILayout.Width(50))) {
                            var matches = new List<GameObject>(receivers.Count);

                            foreach (var t in receivers) {
                                matches.Add(t.gameObject);
                            }
                            Selection.objects = matches.ToArray();
                        }
                    }

                    if (GUILayout.Button("Fire!", EditorStyles.toolbarButton, GUILayout.Width(50))) {
                        MasterAudio.FireCustomEvent(anEvent.EventName, _sounds.transform.position);
                    }

                    GUI.contentColor = DTGUIHelper.BrightButtonColor;
                    GUILayout.Label(string.Format("Receivers: {0}", receivers.Count));
                    GUI.contentColor = Color.white;
                } else {
                    var newName = GUILayout.TextField(anEvent.ProspectiveName, GUILayout.Width(170));
                    if (newName != anEvent.ProspectiveName) {
                        UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Proposed Event Name");
                        anEvent.ProspectiveName = newName;
                    }

                    var buttonPressed = DTGUIHelper.AddDeleteIcon(true, "Custom Event");

                    switch (buttonPressed) {
                        case DTGUIHelper.DTFunctionButtons.Remove:
                            customEventToDelete = i;
                            break;
                        case DTGUIHelper.DTFunctionButtons.Rename:
                            eventToRename = i;
                            break;
                    }
                }

                EditorGUILayout.EndHorizontal();
                EditorGUILayout.EndVertical();

                if (!anEvent.eventExpanded) {
                    EditorGUILayout.EndVertical();
                    continue;
                }

                EditorGUI.indentLevel = 0;
                var rcvMode = (MasterAudio.CustomEventReceiveMode)EditorGUILayout.EnumPopup("Send To Receivers", anEvent.eventReceiveMode);
                if (rcvMode != anEvent.eventReceiveMode) {
                    UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Send To Receivers");
                    anEvent.eventReceiveMode = rcvMode;
                }

                if (rcvMode == MasterAudio.CustomEventReceiveMode.WhenDistanceLessThan || rcvMode == MasterAudio.CustomEventReceiveMode.WhenDistanceMoreThan) {
                    var newDist = EditorGUILayout.Slider("Distance Threshold", anEvent.distanceThreshold, 0f, float.MaxValue);
                    if (newDist != anEvent.distanceThreshold) {
                        UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "change Distance Threshold");
                        anEvent.distanceThreshold = newDist;
                    }
                }
                EditorGUILayout.EndVertical();
            }

            if (customEventToDelete.HasValue) {
                _sounds.customEvents.RemoveAt(customEventToDelete.Value);
            }
            if (eventToRename.HasValue) {
                RenameEvent(_sounds.customEvents[eventToRename.Value]);
            }

            DTGUIHelper.EndGroupedControls();
        }

        // Custom Events End

        if (GUI.changed || _isDirty) {
            EditorUtility.SetDirty(target);
        }

        //DrawDefaultInspector();
    }

#if UNITY_5_0
    private static void SetSpatialBlendsForPlaylistControllers() {
        foreach (var t in PlaylistController.Instances) {
            t.SetSpatialBlend();
        }
    }
#endif

    private void AddSongToPlaylist(MasterAudio.Playlist pList, AudioClip aClip) {
        MusicSetting lastClip = null;
        if (pList.MusicSettings.Count > 0) {
            lastClip = pList.MusicSettings[pList.MusicSettings.Count - 1];
        }

        MusicSetting mus;

        UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "add Song");

        if (lastClip != null && lastClip.clip == null && pList.bulkLocationMode == MasterAudio.AudioLocation.Clip) {
            mus = lastClip;
            mus.clip = aClip;
        } else if (lastClip != null && string.IsNullOrEmpty(lastClip.resourceFileName) && pList.bulkLocationMode == MasterAudio.AudioLocation.ResourceFile) {
            mus = lastClip;
            var unused = false;
            var resourceFileName = DTGUIHelper.GetResourcePath(aClip, ref unused);
            if (string.IsNullOrEmpty(resourceFileName)) {
                // ReSharper disable once RedundantAssignment
                resourceFileName = aClip.name;
            } else {
                mus.resourceFileName = resourceFileName;
            }
        } else {
            mus = new MusicSetting() {
                volume = 1f,
                pitch = 1f,
                isExpanded = true,
                audLocation = pList.bulkLocationMode
            };

            switch (pList.bulkLocationMode) {
                case MasterAudio.AudioLocation.Clip:
                    mus.clip = aClip;
                    if (aClip != null) {
                        mus.songName = aClip.name;
                    }
                    break;
                case MasterAudio.AudioLocation.ResourceFile:
                    var unused = false;
                    var resourceFileName = DTGUIHelper.GetResourcePath(aClip, ref unused);
                    if (string.IsNullOrEmpty(resourceFileName)) {
                        resourceFileName = aClip.name;
                    }

                    mus.clip = null;
                    mus.resourceFileName = resourceFileName;
                    mus.songName = aClip.name;
                    break;
            }

            pList.MusicSettings.Add(mus);
        }
    }

    private static void CreateVariation(Transform groupTrans, AudioClip aClip) {
        var resourceFileName = string.Empty;
        var useLocalization = false;

        if (_sounds.bulkLocationMode == MasterAudio.AudioLocation.ResourceFile) {
            resourceFileName = DTGUIHelper.GetResourcePath(aClip, ref useLocalization);
            if (string.IsNullOrEmpty(resourceFileName)) {
                resourceFileName = aClip.name;
            }
        }

        var newVariation = (GameObject)Instantiate(_sounds.soundGroupVariationTemplate.gameObject, groupTrans.position, Quaternion.identity);

        var clipName = UtilStrings.TrimSpace(aClip.name);

        newVariation.name = clipName;
        newVariation.transform.parent = groupTrans;

        var variation = newVariation.GetComponent<SoundGroupVariation>();

        if (_sounds.bulkLocationMode == MasterAudio.AudioLocation.ResourceFile) {
            variation.audLocation = MasterAudio.AudioLocation.ResourceFile;
            variation.resourceFileName = resourceFileName;
            variation.useLocalization = useLocalization;
        } else {
            variation.VarAudio.clip = aClip;
        }

        CopyFromAudioSourceTemplate(variation.VarAudio, false);

        newVariation.transform.name = clipName;
    }

    private static Transform CreateSoundGroup(AudioClip aClip) {
        var groupName = aClip.name;

        if (_sounds.soundGroupTemplate == null || _sounds.soundGroupVariationTemplate == null) {
            DTGUIHelper.ShowAlert("Your MasterAudio prefab has been altered and cannot function properly. Please Revert it before continuing.");
            return null;
        }

        if (_sounds.transform.FindChild(groupName) != null) {
            DTGUIHelper.ShowAlert("You already have a Sound Group named '" + groupName + "'. Please rename one of them when finished.");
        }

        var resourceFileName = string.Empty;
        var useLocalization = false;

        if (_sounds.bulkLocationMode == MasterAudio.AudioLocation.ResourceFile) {
            resourceFileName = DTGUIHelper.GetResourcePath(aClip, ref useLocalization);
            if (string.IsNullOrEmpty(resourceFileName)) {
                resourceFileName = aClip.name;
            }
        }

        var newGroup = (GameObject)Instantiate(_sounds.soundGroupTemplate.gameObject, _sounds.transform.position, Quaternion.identity);

        var grp = newGroup.GetComponent<MasterAudioGroup>();

#if UNITY_5_0
        if (_sounds.mixerSpatialBlendType == MasterAudio.AllMixerSpatialBlendType.AllowDifferentPerGroup) {
            grp.spatialBlendType = _sounds.newGroupSpatialType;
            grp.spatialBlend = _sounds.newGroupSpatialBlend;
        }
#endif

        var groupTrans = newGroup.transform;
        groupTrans.name = UtilStrings.TrimSpace(groupName);

        var sName = groupName;

        var newVariation = (GameObject)Instantiate(_sounds.soundGroupVariationTemplate.gameObject, groupTrans.position, Quaternion.identity);

        var variation = newVariation.GetComponent<SoundGroupVariation>();

        if (_sounds.bulkLocationMode == MasterAudio.AudioLocation.ResourceFile) {
            variation.audLocation = MasterAudio.AudioLocation.ResourceFile;
            variation.resourceFileName = resourceFileName;
            variation.useLocalization = useLocalization;
            grp.bulkVariationMode = MasterAudio.AudioLocation.ResourceFile;
        } else {
            variation.VarAudio.clip = aClip;
        }

        CopyFromAudioSourceTemplate(variation.VarAudio, false);

        newVariation.transform.name = sName;
        newVariation.transform.parent = groupTrans;

        groupTrans.parent = _sounds.transform;

        MasterAudioGroupInspector.RescanChildren(grp);

        return groupTrans;
    }

    private static MasterAudioGroup CreateSoundGroupFromTemplate(AudioClip aClip, int groupTemplateIndex) {
        var groupName = aClip.name;

        if (_sounds.transform.FindChild(groupName) != null) {
            DTGUIHelper.ShowAlert("You already have a Sound Group named '" + groupName + "'. Please rename one of them when finished.");
        }

        var resourceFileName = string.Empty;
        var useLocalization = false;

        if (_sounds.bulkLocationMode == MasterAudio.AudioLocation.ResourceFile) {
            resourceFileName = DTGUIHelper.GetResourcePath(aClip, ref useLocalization);
            if (string.IsNullOrEmpty(resourceFileName)) {
                resourceFileName = aClip.name;
            }
        }

        var newGroup = (GameObject)Instantiate(_sounds.groupTemplates[groupTemplateIndex], _sounds.transform.position, Quaternion.identity);

        var grp = newGroup.GetComponent<MasterAudioGroup>();

#if UNITY_5_0
        if (_sounds.mixerSpatialBlendType == MasterAudio.AllMixerSpatialBlendType.AllowDifferentPerGroup) {
            grp.spatialBlendType = _sounds.newGroupSpatialType;
            grp.spatialBlend = _sounds.newGroupSpatialBlend;
        }
#endif

        var groupTrans = newGroup.transform;
        groupTrans.name = UtilStrings.TrimSpace(groupName);

        var sName = groupName;

        var audSrcTemplate = SelectedAudioSourceTemplate;

        for (var i = 0; i < newGroup.transform.childCount; i++) {
            var aVar = newGroup.transform.GetChild(i);

            if (aVar.name == "Silence") {
                continue; // no clip
            }

            var variation = aVar.GetComponent<SoundGroupVariation>();

            if (_sounds.bulkLocationMode == MasterAudio.AudioLocation.ResourceFile) {
                variation.audLocation = MasterAudio.AudioLocation.ResourceFile;
                variation.resourceFileName = resourceFileName;
                variation.useLocalization = useLocalization;
                grp.bulkVariationMode = MasterAudio.AudioLocation.ResourceFile;
            } else {
                variation.VarAudio.clip = aClip;
            }

            if (audSrcTemplate != null) {
                var varAudio = variation.VarAudio;
                CopyFromAudioSourceTemplate(varAudio, false);
            }

            variation.transform.name = sName + (i + 1);
        }

        groupTrans.parent = _sounds.transform;

        return grp;
    }

    private void CreateBus(int groupIndex) {
        var sourceGroup = groups[groupIndex];

        var affectedObjects = new Object[] {
			_sounds,
			sourceGroup
		};

        UndoHelper.RecordObjectsForUndo(affectedObjects, "create Bus");

        var newBus = new GroupBus() {
            busName = RenameMeBusName
        };
        _sounds.groupBuses.Add(newBus);

        sourceGroup.busIndex = MasterAudio.HardCodedBusOptions + _sounds.groupBuses.Count - 1;
    }

    private void SoloBus(int busIndex) {
        var bus = _sounds.groupBuses[busIndex];

        var willSolo = !bus.isSoloed;

        var affectedGroups = new List<MasterAudioGroup>();

        foreach (var t in groups) {
            var aGroup = t;

            if (aGroup.busIndex != MasterAudio.HardCodedBusOptions + busIndex) {
                continue;
            }

            affectedGroups.Add(aGroup);
        }

        var allObjects = new List<Object> { _sounds };

        foreach (var g in affectedGroups) {
            allObjects.Add(g);
        }

        UndoHelper.RecordObjectsForUndo(allObjects.ToArray(), "solo Bus");

        //change everything
        bus.isSoloed = willSolo;
        if (bus.isSoloed) {
            bus.isMuted = false;
        }

        foreach (var g in affectedGroups) {
            var sType = g.name;

            if (Application.isPlaying) {
                if (willSolo) {
                    MasterAudio.UnsoloGroup(sType);
                } else {
                    MasterAudio.SoloGroup(sType);
                }
            }

            g.isSoloed = willSolo;
            if (willSolo) {
                g.isMuted = false;
            }
        }

    }

    private void MuteBus(int busIndex) {
        var bus = _sounds.groupBuses[busIndex];

        var willMute = !bus.isMuted;

        var affectedGroups = new List<MasterAudioGroup>();

        foreach (var aGroup in groups) {
            if (aGroup.busIndex != MasterAudio.HardCodedBusOptions + busIndex) {
                continue;
            }

            affectedGroups.Add(aGroup);
        }

        var allObjects = new List<Object> { _sounds };
        foreach (var g in affectedGroups) {
            allObjects.Add(g);
        }

        UndoHelper.RecordObjectsForUndo(allObjects.ToArray(), "mute Bus");

        // change everything
        bus.isMuted = willMute;

        if (bus.isSoloed) {
            bus.isSoloed = false;
        }

        foreach (var g in affectedGroups) {
            if (Application.isPlaying) {
                if (!willMute) {
                    MasterAudio.UnmuteGroup(g.name);
                } else {
                    MasterAudio.MuteGroup(g.name);
                }
            } else {
                g.isMuted = willMute;
                if (bus.isMuted) {
                    g.isSoloed = false;
                }
            }
        }
    }

    private void DeleteBus(int busIndex) {
        var groupsWithBus = new List<MasterAudioGroup>();
        var groupsWithHigherBus = new List<MasterAudioGroup>();

        foreach (var aGroup in groups) {
            if (aGroup.busIndex == -1) {
                continue;
            }
            if (aGroup.busIndex == busIndex + MasterAudio.HardCodedBusOptions) {
                groupsWithBus.Add(aGroup);
            } else if (aGroup.busIndex > busIndex + MasterAudio.HardCodedBusOptions) {
                groupsWithHigherBus.Add(aGroup);
            }
        }

        var allObjects = new List<Object> { _sounds };
        foreach (var g in groupsWithBus) {
            allObjects.Add(g);
        }

        foreach (var g in groupsWithHigherBus) {
            allObjects.Add(g);
        }

        UndoHelper.RecordObjectsForUndo(allObjects.ToArray(), "delete Bus");

        // change all
        _sounds.groupBuses.RemoveAt(busIndex);

        foreach (var group in groupsWithBus) {
            group.busIndex = -1;
        }

        foreach (var group in groupsWithHigherBus) {
            group.busIndex--;
        }
    }

    private void ExpandCollapseAllPlaylists(bool expand) {
        UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "toggle Expand / Collapse Playlists");

        foreach (var aList in _sounds.musicPlaylists) {
            aList.isExpanded = expand;
        }
    }

    private void ScanGroups() {
        groups.Clear();

        var names = new List<string>();

        for (var i = 0; i < _sounds.transform.childCount; i++) {
            var aChild = _sounds.transform.GetChild(i);
            if (names.Contains(aChild.name)) {
                DTGUIHelper.ShowRedError("You have more than one group named '" + aChild.name + "'.");
                DTGUIHelper.ShowRedError("Please rename one of them before continuing.");
                _isValid = false;
                return;
            }

            names.Add(aChild.name);
            var aGroup = aChild.GetComponent<MasterAudioGroup>();

            if (aGroup == null) {
                Debug.LogError("No MasterAudioGroup script found on Group '" + aChild.name + "'. This should never happen. Please delete and recreate this Group.");
                continue;
            }

            groups.Add(aGroup);
        }

        if (_sounds.groupByBus) {
            groups.Sort(delegate(MasterAudioGroup g1, MasterAudioGroup g2) {
                // ReSharper disable once ConvertIfStatementToReturnStatement
                if (g1.busIndex == g2.busIndex) {
                    return g1.name.CompareTo(g2.name);
                }

                return g1.busIndex.CompareTo(g2.busIndex);
            });
        } else {
            groups.Sort(delegate(MasterAudioGroup g1, MasterAudioGroup g2) {
                return g1.name.CompareTo(g2.name);
            });
        }
    }

    private List<string> GroupNameList {
        get {
            var groupNames = new List<string> { MasterAudio.NoGroupName };

            foreach (var t in groups) {
                groupNames.Add(t.name);
            }

            return groupNames;
        }
    }

    private void DisplayJukebox() {
        EditorGUILayout.Separator();

        var pcs = PlaylistController.Instances;

        var songNames = new List<string>();

        foreach (var t in pcs) {
            var pl = t;

            GUI.backgroundColor = Color.white;
            GUI.color = DTGUIHelper.ActiveHeaderColor;

            EditorGUILayout.BeginVertical(DTGUIHelper.CornerGUIStyle);
            EditorGUILayout.BeginVertical();
            EditorGUILayout.BeginHorizontal();

            GUI.color = DTGUIHelper.BrightButtonColor;
            var playlistIndex = -1;
            if (!string.IsNullOrEmpty(pl.startPlaylistName) && pl.HasPlaylist && pl.CurrentPlaylist != null) {
                playlistIndex = _playlistNames.IndexOf(pl.CurrentPlaylist.playlistName);

                songNames.Clear();
                foreach (var aSong in pl.CurrentPlaylist.MusicSettings) {
                    var songName = string.Empty;

                    switch (aSong.audLocation) {
                        case MasterAudio.AudioLocation.Clip:
                            songName = aSong.clip == null ? string.Empty : aSong.clip.name;
                            break;
                        case MasterAudio.AudioLocation.ResourceFile:
                            songName = aSong.resourceFileName;
                            break;
                    }

                    if (string.IsNullOrEmpty(songName)) {
                        continue;
                    }

                    songNames.Add(songName);
                }
            }

            GUILayout.Label(pl.name);

            GUILayout.FlexibleSpace();

            GUILayout.Label("V " + pl._playlistVolume.ToString("N2"));

            var newVol = GUILayout.HorizontalSlider(pl._playlistVolume, 0f, 1f, GUILayout.Width(100));
            // ReSharper disable once CompareOfFloatsByEqualityOperator
            if (newVol != pl._playlistVolume) {
                pl.PlaylistVolume = newVol;
            }

            GUI.color = Color.white;
            var muteButtonPressed = DTGUIHelper.AddPlaylistControllerSetupButtons(pl, "Playlist Controller", true);

            if (muteButtonPressed == DTGUIHelper.DTFunctionButtons.Mute) {
                pl.ToggleMutePlaylist();
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();

            GUI.backgroundColor = DTGUIHelper.BrightButtonColor;
            GUI.color = DTGUIHelper.BrightButtonColor;

            EditorGUILayout.BeginVertical(DTGUIHelper.CornerGUIStyle);

            var clip = pl.CurrentPlaylistClip;
            var clipPosition = "";
            AudioSource playingSource = null;
            if (clip != null) {
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                playingSource = pl == null ? null : pl.CurrentPlaylistSource;
                if (playingSource != null) {
                    clipPosition = "(-" + (clip.length - playingSource.time).ToString("N2") + " secs)";
                }
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Playlist:");
            GUILayout.Space(30);
            var playlistIndexToStart = EditorGUILayout.Popup(playlistIndex, _playlistNames.ToArray(), GUILayout.Width(150));

            if (playlistIndex != playlistIndexToStart) {
                pl.ChangePlaylist(_playlistNames[playlistIndexToStart]);
            }
            GUILayout.Label(string.Format("[{0}]", pl.PlaylistState));
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Active Clip:");
            GUILayout.Space(9);

            var songIndex = -1;
            if (pl.CurrentPlaylistClip != null) {
                songIndex = songNames.IndexOf(pl.CurrentPlaylistClip.name);
            }
            var newSong = EditorGUILayout.Popup(songIndex, songNames.ToArray(), GUILayout.Width(150));
            if (newSong != songIndex) {
                pl.TriggerPlaylistClip(songNames[newSong]);
            }

            if (!string.IsNullOrEmpty(clipPosition)) {
                GUILayout.Label(clipPosition);
            }
            GUILayout.FlexibleSpace();
            EditorGUILayout.EndHorizontal();

            // ReSharper disable once ConditionIsAlwaysTrueOrFalse
            var fadingClip = pl == null ? null : pl.FadingPlaylistClip;
            var fadingClipName = fadingClip == null ? "[None]" : fadingClip.name;
            var fadingClipPosition = "";
            if (fadingClip != null) {
                // ReSharper disable once ConditionIsAlwaysTrueOrFalse
                var fadingSource = pl == null ? null : pl.FadingSource;
                if (fadingSource != null) {
                    fadingClipPosition = "(-" + (fadingClip.length - fadingSource.time).ToString("N2") + " secs)";
                }
            }

            EditorGUILayout.BeginHorizontal();
            GUILayout.Label("Fading Clip:");
            GUILayout.Space(7);
            GUILayout.Label(fadingClipName + "  " + fadingClipPosition);
            GUILayout.FlexibleSpace();

            EditorGUILayout.EndHorizontal();
            GUI.backgroundColor = Color.white;
            GUI.color = DTGUIHelper.BrightButtonColor;

            // ReSharper disable once RedundantAssignment
            var buttonPressed = DTGUIHelper.JukeboxButtons.None;

            EditorGUILayout.BeginHorizontal(EditorStyles.toolbarButton);
            buttonPressed = DTGUIHelper.AddJukeboxIcons();
            if (playingSource != null) {
                var oldtime = playingSource.time;
                var newTime = EditorGUILayout.Slider("", oldtime, 0f, clip.length);
                if (oldtime != newTime) {
                    playingSource.time = newTime;
                }
            }

            EditorGUILayout.EndHorizontal();

            EditorGUILayout.EndVertical();
            EditorGUILayout.EndVertical();

            GUI.color = Color.white;
            GUI.backgroundColor = Color.white;
            EditorGUILayout.Separator();

            switch (buttonPressed) {
                case DTGUIHelper.JukeboxButtons.Stop:
                    pl.StopPlaylist();
                    break;
                case DTGUIHelper.JukeboxButtons.NextSong:
                    pl.PlayTheNextSong(PlaylistController.AudioPlayType.PlayNow, true);
                    break;
                case DTGUIHelper.JukeboxButtons.Pause:
                    pl.PausePlaylist();
                    break;
                case DTGUIHelper.JukeboxButtons.Play:
                    if (!pl.ResumePlaylist()) {
                        if (pl.CurrentPlaylist != null) {
                            pl.StartPlaylist(pl.CurrentPlaylist.playlistName);
                        }
                    }
                    break;
                case DTGUIHelper.JukeboxButtons.RandomSong:
                    pl.PlayARandomSong(PlaylistController.AudioPlayType.PlayNow, true);
                    break;
            }
        }
    }

    private void BusMuteSoloReset() {
        var allObjects = new List<Object> { _sounds };
        foreach (var g in groups) {
            allObjects.Add(g);
        }

        UndoHelper.RecordObjectsForUndo(allObjects.ToArray(), "Mute/Solo Reset");

        //reset everything

        foreach (var aBus in _sounds.groupBuses) {
            if (Application.isPlaying) {
                MasterAudio.UnsoloBus(aBus.busName);
                MasterAudio.UnmuteBus(aBus.busName);
            } else {
                aBus.isSoloed = false;
                aBus.isMuted = false;
            }
        }

        foreach (var gr in groups) {
            if (Application.isPlaying) {
                MasterAudio.UnsoloGroup(gr.name);
                MasterAudio.UnmuteGroup(gr.name);
            } else {
                gr.isSoloed = false;
                gr.isMuted = false;
            }
        }
    }

    private void EqualizePlaylistVolumes(List<MusicSetting> playlistClips) {
        var clips = new Dictionary<MusicSetting, float>();

        if (playlistClips.Count < 2) {
            DTGUIHelper.ShowAlert("You must have at least 2 clips in a Playlist to use this function.");
            return;
        }

        var lowestVolume = 1f;

        foreach (var setting in playlistClips) {
            var ac = setting.clip;

            if (setting.audLocation == MasterAudio.AudioLocation.Clip && ac == null) {
                continue;
            }

            if (setting.audLocation == MasterAudio.AudioLocation.ResourceFile) {
                ac = Resources.Load(setting.resourceFileName) as AudioClip;
                if (ac == null) {
                    Debug.LogError("Song '" + setting.resourceFileName + "' could not be loaded and is being skipped.");
                    continue;
                }
            }

            var average = 0f;
            var buffer = new float[ac.samples];

            Debug.Log("Measuring amplitude of '" + ac.name + "'.");

            try {
                ac.GetData(buffer, 0);
            }
            catch {
                Debug.Log("Could not read data from compressed sample. Skipping '" + setting.clip.name + "'.");
                continue;
            }

            for (var c = 0; c < ac.samples; c++) {
                average += Mathf.Pow(buffer[c], 2);
            }

            // ReSharper disable once RedundantCast
            average = Mathf.Sqrt(1f / (float)ac.samples * average);

            if (average == 0) {
                Debug.LogError("Song '" + setting.songName + "' is being excluded because it's compressed or streaming.");
                continue;
            }

            if (average < lowestVolume) {
                lowestVolume = average;
            }

            clips.Add(setting, average);
        }

        if (clips.Count < 2) {
            DTGUIHelper.ShowAlert("You must have at least 2 clips in a Playlist to use this function.");
            return;
        }

        UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "Equalize Song Volumes");

        foreach (var kv in clips) {
            var adjustedVol = lowestVolume / kv.Value;
            //set your volume for each song in your playlist.
            kv.Key.volume = adjustedVol;
        }
    }

    private static void CreateCustomEvent(string newEventName) {
        if (_sounds.customEvents.FindAll(delegate(CustomEvent obj) {
            return obj.EventName == newEventName;
        }).Count > 0) {
            DTGUIHelper.ShowAlert("You already have a Custom Event named '" + newEventName + "'. Please choose a different name.");
            return;
        }

        _sounds.customEvents.Add(new CustomEvent(newEventName));
    }

    private static void RenameEvent(CustomEvent cEvent) {
        var match = _sounds.customEvents.FindAll(delegate(CustomEvent obj) {
            return obj.EventName == cEvent.ProspectiveName;
        });

        if (match.Count > 0) {
            DTGUIHelper.ShowAlert("You already have a Custom Event named '" + cEvent.ProspectiveName + "'. Please choose a different name.");
            return;
        }

        cEvent.EventName = cEvent.ProspectiveName;
    }

    private void AddGroupTemplate(GameObject temp) {
        if (_groupTemplateNames.Contains(temp.name)) {
            Debug.LogError("There is already a Sound Group template named '" + temp.name + "'. The names of templates must be unique.");
            return;
        }

        if (temp.GetComponent<MasterAudioGroup>() == null) {
            Debug.LogError("This is not a Sound Group template. It must have a Master Audio Group script in the top-level.");
            return;
        }

        var hasVariation = false;
        if (temp.transform.childCount > 0) {
            var aChild = temp.transform.GetChild(0);
            hasVariation = aChild.GetComponent<SoundGroupVariation>() != null;
        }

        if (!hasVariation) {
            Debug.LogError("This is not a Sound Group template. It must have a at least one child with a Sound Group Variation script in it.");
            return;
        }

        var hasAudioClips = false;
        for (var i = 0; i < temp.transform.childCount; i++) {
            var aChild = temp.transform.GetChild(i);
            var aud = aChild.transform.GetComponent<AudioSource>();
            if (aud == null) {
                continue;
            }

            if (aud.clip != null) {
                hasAudioClips = true;
            }
        }

        if (hasAudioClips) {
            Debug.LogError("Sound Group templates cannot include any Audio Clips in their Variations. Please remove them and try again.");
            return;
        }

        var hasFilters = false;
        for (var i = 0; i < temp.transform.childCount; i++) {
            var aChild = temp.transform.GetChild(i);

            if (aChild.GetComponent<AudioDistortionFilter>() != null
                || aChild.GetComponent<AudioHighPassFilter>() != null
                || aChild.GetComponent<AudioLowPassFilter>() != null
                || aChild.GetComponent<AudioReverbFilter>() != null
                || aChild.GetComponent<AudioEchoFilter>() != null
                || aChild.GetComponent<AudioChorusFilter>() != null) {

                hasFilters = true;
            }
        }

        if (hasFilters) {
            Debug.LogError("Sound Group templates cannot include any Filter FX in their Variations. Please remove them and try again.");
            return;
        }


        UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "Add Group Template");
        _sounds.groupTemplates.Add(temp.gameObject);
        _sounds.groupTemplates.Sort(delegate(GameObject x, GameObject y) {
            return x.name.CompareTo(y.name);
        });

        Debug.Log("Added Group Template '" + temp.name + "'");
    }

    private void AddAudioSourceTemplate(GameObject temp) {
        if (_audioSourceTemplateNames.Contains(temp.name)) {
            Debug.LogError("There is already an Audio Source Template named '" + temp.name + "'. The names of Templates must be unique.");
            return;
        }

        if (temp.GetComponent<AudioSource>() == null) {
            Debug.LogError("This is not an Audio Source Template. It must have an Audio Source component in the top-level.");
            return;
        }

        var hasAudioClips = false;
        for (var i = 0; i < temp.transform.childCount; i++) {
            var aChild = temp.transform.GetChild(i);
            var aud = aChild.transform.GetComponent<AudioSource>();
            if (aud == null) {
                continue;
            }

            if (aud.clip != null) {
                hasAudioClips = true;
            }
        }

        if (hasAudioClips) {
            Debug.LogError("Audio Source Templates cannot include any Audio Clips. Please remove them and try again.");
            return;
        }

        var hasFilters = false;
        for (var i = 0; i < temp.transform.childCount; i++) {
            var aChild = temp.transform.GetChild(i);

            if (aChild.GetComponent<AudioDistortionFilter>() != null
                || aChild.GetComponent<AudioHighPassFilter>() != null
                || aChild.GetComponent<AudioLowPassFilter>() != null
                || aChild.GetComponent<AudioReverbFilter>() != null
                || aChild.GetComponent<AudioEchoFilter>() != null
                || aChild.GetComponent<AudioChorusFilter>() != null) {

                hasFilters = true;
            }
        }

        if (hasFilters) {
            Debug.LogError("Audio Source Templates cannot include any Filter FX in their Variations. Please remove them and try again.");
            return;
        }

        UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "Add Audio Source Template");
        _sounds.audioSourceTemplates.Add(temp.gameObject);
        _sounds.audioSourceTemplates.Sort(delegate(GameObject x, GameObject y) {
            return x.name.CompareTo(y.name);
        });

        Debug.Log("Added Audio Source Template '" + temp.name + "'");
    }

    private void SortCustomEvents() {
        UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "Sort Custom Events Alpha");

        _sounds.customEvents.Sort(delegate(CustomEvent x, CustomEvent y) {
            return x.EventName.CompareTo(y.EventName);
        });
    }

    private void ExpandCollapseCustomEvents(bool shouldExpand) {
        UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "Expand / Collapse All Custom Events");

        foreach (var t in _sounds.customEvents) {
            t.eventExpanded = shouldExpand;
        }
    }

    private void SortSongsAlpha(MasterAudio.Playlist aList) {
        UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "Sort Playlist Songs Alpha");

        aList.MusicSettings.Sort(delegate(MusicSetting x, MusicSetting y) {
            return x.songName.CompareTo(y.songName);
        });
    }

    private void ExpandCollapseSongs(MasterAudio.Playlist aList, bool shouldExpand) {
        UndoHelper.RecordObjectPropertyForUndo(ref _isDirty, _sounds, "Expand / Collapse Playlist Songs");

        foreach (var t in aList.MusicSettings) {
            t.isExpanded = shouldExpand;
        }
    }

    private void StopPreviewer() {
        GetPreviewer().Stop();
    }

    private AudioSource GetPreviewer() {
        var aud = _previewer.GetComponent<AudioSource>();
        if (aud != null) {
            return aud;
        }

#if UNITY_3_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5 || UNITY_4_0
        _previewer.AddComponent<AudioSource>();
#else
        UnityEditorInternal.ComponentUtility.CopyComponent(_sounds.soundGroupVariationTemplate.GetComponent<AudioSource>());
        UnityEditorInternal.ComponentUtility.PasteComponentAsNew(_previewer);
#endif

        aud = _previewer.GetComponent<AudioSource>();

        return aud;
    }

    private static GroupBus GetGroupBus(MasterAudioGroup aGroup) {
        GroupBus groupBus = null;
        var groupBusIndex = aGroup.busIndex - MasterAudio.HardCodedBusOptions;
        if (groupBusIndex >= 0 && groupBusIndex < _sounds.groupBuses.Count) {
            groupBus = _sounds.groupBuses[groupBusIndex];
        }

        return groupBus;
    }

    private static bool MuteMixerGroup(MasterAudioGroup aGroup, ref bool groupDirty, bool isBulk, bool wasWarningShown) {
        var groupBus = GetGroupBus(aGroup);

        var warningShown = false;

        if (groupBus != null && (groupBus.isMuted || groupBus.isSoloed)) {
            if (wasWarningShown) {
                return false;
            }

            if (Application.isPlaying) {
                Debug.LogWarning(NoMuteSoloAllowed);
                warningShown = true;
            } else {
                DTGUIHelper.ShowAlert(NoMuteSoloAllowed);
                warningShown = true;
            }
        } else {
            if (!isBulk) {
                UndoHelper.RecordObjectPropertyForUndo(ref groupDirty, aGroup, "toggle Group mute");
            }

            if (Application.isPlaying) {
                var sType = aGroup.name;

                if (aGroup.isMuted) {
                    MasterAudio.UnmuteGroup(sType);
                } else {
                    MasterAudio.MuteGroup(sType);
                }
            } else {
                aGroup.isMuted = !aGroup.isMuted;
                if (aGroup.isMuted) {
                    aGroup.isSoloed = false;
                }
            }
        }

        return warningShown;
    }

    private static bool SoloMixerGroup(MasterAudioGroup aGroup, ref bool groupDirty, bool isBulk, bool wasWarningShown) {
        var groupBus = GetGroupBus(aGroup);

        var warningShown = false;

        if (groupBus != null && (groupBus.isMuted || groupBus.isSoloed)) {
            if (wasWarningShown) {
                return false;
            }
            if (Application.isPlaying) {
                Debug.LogWarning(NoMuteSoloAllowed);
                warningShown = true;
            } else {
                DTGUIHelper.ShowAlert(NoMuteSoloAllowed);
                warningShown = true;
            }
        } else {
            if (!isBulk) {
                UndoHelper.RecordObjectPropertyForUndo(ref groupDirty, aGroup, "toggle Group solo");
            }

            if (Application.isPlaying) {
                var sType = aGroup.name;

                if (aGroup.isSoloed) {
                    MasterAudio.UnsoloGroup(sType);
                } else {
                    MasterAudio.SoloGroup(sType);
                }
            } else {
                aGroup.isSoloed = !aGroup.isSoloed;
                if (aGroup.isSoloed) {
                    aGroup.isMuted = false;
                }
            }
        }

        return warningShown;
    }

    private static void SetMixerGroupVolume(MasterAudioGroup aGroup, ref bool groupDirty, float newVol, bool isBulk) {
        if (!isBulk) {
            UndoHelper.RecordObjectPropertyForUndo(ref groupDirty, aGroup, "change Group Volume");
        }

        aGroup.groupMasterVolume = newVol;
        if (Application.isPlaying) {
            MasterAudio.SetGroupVolume(aGroup.name, aGroup.groupMasterVolume);
        }
    }

    private static void SelectActiveVariationsInGroup(MasterAudioGroup aGroup) {
        var actives = new List<GameObject>(aGroup.transform.childCount);

        for (var i = 0; i < aGroup.transform.childCount; i++) {
            var aVar = aGroup.transform.GetChild(i).GetComponent<SoundGroupVariation>();
            if (!aVar.IsPlaying) {
                continue;
            }

            actives.Add(aVar.GameObj);
        }

        if (actives.Count == 0) {
            return;
        }

        Selection.objects = actives.ToArray();
    }

    private static void SelectActiveVariationsInBus(GroupBus aBus) {
        var objects = MasterAudio.GetAllPlayingVariationsInBus(aBus.busName);
        Selection.objects = objects.ToArray();
    }

    private static GameObject SelectedAudioSourceTemplate {
        get {
            if (MasterAudio.Instance.audioSourceTemplates.Count == 0) {
                return null;
            }

            var selTemplate = MasterAudio.Instance.audioSourceTemplates.Find(delegate(GameObject obj) {
                return obj.name == MasterAudio.Instance.audioSourceTemplateName;
            });

            return selTemplate;
        }
    }

    public static void CopyFromAudioSourceTemplate(AudioSource oldAudSrc, bool showError) {
#if UNITY_3_0 || UNITY_3_1 || UNITY_3_2 || UNITY_3_3 || UNITY_3_4 || UNITY_3_5 || UNITY_4_0
        Debug.LogError("This feature is only supported in Unity 4.1+");
#else
        var selSource = SelectedAudioSourceTemplate;
        if (selSource == null) {
            if (showError) {
                Debug.LogError("No Audio Source Template selected.");
            }
            return;
        }

        var templateAudio = selSource.GetComponent<AudioSource>();

        var oldPitch = oldAudSrc.pitch;
        var oldLoop = oldAudSrc.loop;
        var oldClip = oldAudSrc.clip;
        var oldVol = oldAudSrc.volume;

        UnityEditorInternal.ComponentUtility.CopyComponent(templateAudio);
        UnityEditorInternal.ComponentUtility.PasteComponentValues(oldAudSrc);

        oldAudSrc.pitch = oldPitch;
        oldAudSrc.loop = oldLoop;
        oldAudSrc.clip = oldClip;
        oldAudSrc.volume = oldVol;
#endif
    }
}
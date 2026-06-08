using System;
using System.Collections.Generic;
using System.IO;
using System.Globalization;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
using Monobehaviours.Singletons;
using Models.Gameplay.Campaign;
using Services;

namespace Models.CampaignEditor
{
    public class CampaignLoadEditorMode : EditorMode
    {
        private readonly TilemapEditor _editor;
        private readonly string _folderRelative;

        private Label _folderLabel;
        private Button _refreshButton;
        private Button _saveButton;
        private ListView _listView;
        private TextField _campaignStartTimeField;
        private IntegerField _simulationTickMinutesField;
        private IntegerField _operationalCadenceHoursField;
        private Label _settingsStatusLabel;

        // Popup elements
        private VisualElement _popupOverlay;
        
        // Load confirmation popup
        private VisualElement _loadConfirmPopup;
        private Label _loadFilenameLabel;
        private Button _loadCancelBtn;
        private Button _loadConfirmBtn;
        
        // Save name popup
        private VisualElement _saveNamePopup;
        private TextField _saveNameField;
        private Button _saveCancelBtn;
        private Button _saveConfirmBtn;

        private readonly List<string> _filePaths = new List<string>();
        private string FolderFullPath => Path.Combine(Application.persistentDataPath, _folderRelative);

        // Store the pending load path
        private string _pendingLoadPath;

        public CampaignLoadEditorMode(VisualElement tab, TilemapEditor editor, TileHighlighter _highlighter, string folderRelative)
            : base(tab, editor, _highlighter)
        {
            _editor = editor;
            _folderRelative = string.IsNullOrWhiteSpace(folderRelative) ? "Campaigns" : folderRelative;

            CacheUI();
            WireUI();
            
            RefreshList();
        }

        private void CacheUI()
        {
            _folderLabel = _tab.Q<Label>("campaign-folder-label");
            _refreshButton = _tab.Q<Button>("refresh-campaign-list-btn");
            _listView = _tab.Q<ListView>("campaign-listview");
            _saveButton = _tab.Q<Button>("save-btn");
            _campaignStartTimeField = _tab.Q<TextField>("campaign-start-time-field");
            _simulationTickMinutesField = _tab.Q<IntegerField>("simulation-tick-minutes-field");
            _operationalCadenceHoursField = _tab.Q<IntegerField>("operational-cadence-hours-field");
            _settingsStatusLabel = _tab.Q<Label>("campaign-settings-status-label");
            
            // Get popup elements from root
            var root = _tab.panel.visualTree;
            _popupOverlay = root.Q<VisualElement>("popup-overlay");
            
            // Load confirmation popup
            _loadConfirmPopup = root.Q<VisualElement>("load-confirm-popup");
            _loadFilenameLabel = root.Q<Label>("load-filename-label");
            _loadCancelBtn = root.Q<Button>("load-cancel-btn");
            _loadConfirmBtn = root.Q<Button>("load-confirm-btn");
            
            // Save name popup
            _saveNamePopup = root.Q<VisualElement>("save-name-popup");
            _saveNameField = root.Q<TextField>("save-name-field");
            _saveCancelBtn = root.Q<Button>("save-cancel-btn");
            _saveConfirmBtn = root.Q<Button>("save-confirm-btn");
            
            if (_listView != null)
            {
                _listView.fixedItemHeight = 30; 

                _listView.makeItem = () =>
                {
                    var row = new VisualElement
                    {
                        style =
                        {
                            flexDirection = FlexDirection.Row,
                            alignItems = Align.Center,
                            paddingLeft = 5,
                            paddingRight = 5
                        }
                    };

                    var nameLabel = new Label { name = "file-name", style = { flexGrow = 1 } };
                    var dateLabel = new Label
                    {
                        name = "file-date",
                        style =
                        {
                            fontSize = 10,
                            color = new StyleColor(Color.gray),
                            minWidth = 100,
                            unityTextAlign = TextAnchor.MiddleRight
                        }
                    };

                    row.Add(nameLabel);
                    row.Add(dateLabel);
                    return row;
                };

                _listView.bindItem = (element, index) =>
                {
                    if (index < 0 || index >= _filePaths.Count) return;

                    var fullPath = _filePaths[index];
                    var nameLabel = element.Q<Label>("file-name");
                    var dateLabel = element.Q<Label>("file-date");

                    if (nameLabel != null) 
                        nameLabel.text = Path.GetFileNameWithoutExtension(fullPath);

                    if (dateLabel != null)
                    {
                        try
                        {
                            var dt = File.GetLastWriteTime(fullPath);
                            dateLabel.text = dt.ToString("yyyy-MM-dd HH:mm");
                        }
                        catch
                        {
                            dateLabel.text = "-";
                        }
                    }
                };

                _listView.itemsSource = _filePaths;
                _listView.selectionType = SelectionType.Single;
                
                _listView.selectionChanged += OnSelectionChanged;
            }
        }

        private void WireUI()
        {
            if (_refreshButton != null)
                _refreshButton.clicked += RefreshList;
                
            if (_saveButton != null)
                _saveButton.clicked += ShowSavePopup;
                
            // Wire load popup buttons
            if (_loadCancelBtn != null)
                _loadCancelBtn.clicked += HideLoadPopup;
                
            if (_loadConfirmBtn != null)
                _loadConfirmBtn.clicked += ConfirmLoad;
                
            // Wire save popup buttons
            if (_saveCancelBtn != null)
                _saveCancelBtn.clicked += HideSavePopup;
                
            if (_saveConfirmBtn != null)
                _saveConfirmBtn.clicked += ConfirmSave;
                
            // Allow Enter key to confirm save
            if (_saveNameField != null)
            {
                _saveNameField.RegisterCallback<KeyDownEvent>(evt =>
                {
                    if (evt.keyCode == KeyCode.Return || evt.keyCode == KeyCode.KeypadEnter)
                    {
                        ConfirmSave();
                        evt.StopPropagation();
                    }
                    else if (evt.keyCode == KeyCode.Escape)
                    {
                        HideSavePopup();
                        evt.StopPropagation();
                    }
                });
            }

            if (_campaignStartTimeField != null)
                _campaignStartTimeField.RegisterValueChangedCallback(_ => ApplySettingsFieldsToCampaign());

            if (_simulationTickMinutesField != null)
                _simulationTickMinutesField.RegisterValueChangedCallback(_ => ApplySettingsFieldsToCampaign());

            if (_operationalCadenceHoursField != null)
                _operationalCadenceHoursField.RegisterValueChangedCallback(_ => ApplySettingsFieldsToCampaign());
        }

        public override void EraseTile(Vector3Int cellPos, Vector3Int? lastPaintedCell)
        {
            
        }

        public override void SetEditorMode()
        {
            base.SetEditorMode();
            RefreshList(); 
        }

        public override void SetCampaign()
        {
            RefreshSettingsFields();
        }

        private void RefreshList()
        {
            try
            {
                if (!Directory.Exists(FolderFullPath))
                    Directory.CreateDirectory(FolderFullPath);

                if (_folderLabel != null)
                    _folderLabel.text = $"Folder: .../{_folderRelative}";

                _filePaths.Clear();

                var files = Directory.GetFiles(FolderFullPath, "*.json", SearchOption.TopDirectoryOnly);
                var activeModuleId = ModuleSingleton.Instance.ActiveModule.Id;

                var sortedFiles = files
                    .Where(file => TemplateMatchesActiveModule(file, activeModuleId))
                    .OrderByDescending(f => File.GetLastWriteTime(f))
                    .ToList();
                _filePaths.AddRange(sortedFiles);

                _listView?.RefreshItems(); 
            }
            catch (Exception e)
            {
                Debug.LogError($"[CampaignLoad] Failed to refresh list: {e.Message}");
            }
        }

        private bool TemplateMatchesActiveModule(string filePath, string activeModuleId)
        {
            if (string.IsNullOrWhiteSpace(activeModuleId))
                return false;

            try
            {
                return CampaignSavingService.TryReadCampaignMetadata(filePath, out var metadata) &&
                       string.Equals(metadata.ModuleId, activeModuleId, StringComparison.OrdinalIgnoreCase);
            }
            catch (Exception e)
            {
                Debug.LogWarning($"[CampaignLoad] Skipping unreadable campaign '{Path.GetFileName(filePath)}': {e.Message}");
                return false;
            }
        }

        private void OnSelectionChanged(IEnumerable<object> selected)
        {
            var selectedPath = selected?.FirstOrDefault() as string;

            if (string.IsNullOrEmpty(selectedPath) || !File.Exists(selectedPath)) 
                return;

            // Store the path and show confirmation popup
            _pendingLoadPath = selectedPath;
            ShowLoadPopup(selectedPath);

            // Clear selection immediately
            _listView.ClearSelection();
        }

        // Load popup methods
        private void ShowLoadPopup(string filePath)
        {
            if (_popupOverlay == null || _loadConfirmPopup == null) return;
            
            if (_loadFilenameLabel != null)
                _loadFilenameLabel.text = $"File: {Path.GetFileName(filePath)}";
            
            _popupOverlay.style.display = DisplayStyle.Flex;
            _loadConfirmPopup.style.display = DisplayStyle.Flex;
            _saveNamePopup.style.display = DisplayStyle.None;
        }

        private void HideLoadPopup()
        {
            if (_popupOverlay == null) return;
            
            _popupOverlay.style.display = DisplayStyle.None;
            _loadConfirmPopup.style.display = DisplayStyle.None;
            _pendingLoadPath = null;
        }

        private void ConfirmLoad()
        {
            if (string.IsNullOrEmpty(_pendingLoadPath))
            {
                HideLoadPopup();
                return;
            }

            if (!TemplateMatchesActiveModule(_pendingLoadPath, ModuleSingleton.Instance.ActiveModule.Id))
            {
                Debug.LogError(
                    $"Cannot load '{Path.GetFileName(_pendingLoadPath)}' because it does not match active module '{ModuleSingleton.Instance.ActiveModule.Id}'.");
                HideLoadPopup();
                RefreshList();
                return;
            }

            Debug.Log($"Loading Campaign: {Path.GetFileName(_pendingLoadPath)}");
            _editor.LoadCampaignFromJson(_pendingLoadPath);
            
            HideLoadPopup();
            RefreshSettingsFields();
        }

        // Save popup methods
        private void ShowSavePopup()
        {
            if (_popupOverlay == null || _saveNamePopup == null) return;

            if (_editor.editingCampaign == null)
            {
                Debug.LogWarning("Create or load a CampaignTemplate template before saving.");
                return;
            }
            
            // Suggest a default name with timestamp
            var defaultName = $"Campaign_{DateTime.Now:yyyyMMdd_HHmmss}";
            if (_saveNameField != null)
            {
                _saveNameField.value = defaultName;
                _saveNameField.Focus();
                // Select all text for easy replacement
                _saveNameField.SelectAll();
            }
            
            _popupOverlay.style.display = DisplayStyle.Flex;
            _saveNamePopup.style.display = DisplayStyle.Flex;
            _loadConfirmPopup.style.display = DisplayStyle.None;
        }

        private void HideSavePopup()
        {
            if (_popupOverlay == null) return;
            
            _popupOverlay.style.display = DisplayStyle.None;
            _saveNamePopup.style.display = DisplayStyle.None;
        }

        private void ConfirmSave()
        {
            if (_saveNameField == null) return;
            
            var campaignName = _saveNameField.value?.Trim();
            
            if (string.IsNullOrEmpty(campaignName))
            {
                Debug.LogWarning("CampaignTemplate name cannot be empty!");
                return;
            }
            
            // Sanitize filename (remove invalid characters)
            var invalidChars = Path.GetInvalidFileNameChars();
            foreach (var c in invalidChars)
            {
                campaignName = campaignName.Replace(c, '_');
            }
            
            // Ensure .json extension
            if (!campaignName.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                campaignName += ".json";
            
            var fullPath = Path.Combine(FolderFullPath, campaignName);
            
            try
            {
                if (_editor.editingCampaign == null)
                {
                    Debug.LogWarning("Create or load a CampaignTemplate template before saving.");
                    return;
                }

                _editor.editingCampaign.ModuleId = ModuleSingleton.Instance.ActiveModule.Id;
                if (!ApplySettingsFieldsToCampaign())
                    return;

                _editor.CaptureReferenceImageIntoCampaign();
                CampaignSavingService.SaveCampaign(Editor.editingCampaign, fullPath);
                Debug.Log($"CampaignTemplate saved: {campaignName}");
                RefreshList();
                HideSavePopup();
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save campaign: {e.Message}");
            }
        }

        private void RefreshSettingsFields()
        {
            var campaign = _editor.editingCampaign;
            var hasCampaign = campaign != null;

            _campaignStartTimeField?.SetEnabled(hasCampaign);
            _simulationTickMinutesField?.SetEnabled(hasCampaign);
            _operationalCadenceHoursField?.SetEnabled(hasCampaign);

            if (!hasCampaign)
            {
                if (_settingsStatusLabel != null)
                    _settingsStatusLabel.text = "Create or load a CampaignTemplate template to edit settings.";
                return;
            }

            campaign.SimulationSettings ??= new SimulationSettings();
            campaign.SimulationSettings.Normalize();

            _campaignStartTimeField?.SetValueWithoutNotify(campaign.CampaignStartTime.ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture));
            _simulationTickMinutesField?.SetValueWithoutNotify(campaign.SimulationSettings.SimulationTickMinutes);
            _operationalCadenceHoursField?.SetValueWithoutNotify(campaign.SimulationSettings.OperationalCadenceHours);

            if (_settingsStatusLabel != null)
                _settingsStatusLabel.text = "Settings ready.";
        }

        private bool ApplySettingsFieldsToCampaign()
        {
            var campaign = _editor.editingCampaign;
            if (campaign == null)
                return true;

            if (_campaignStartTimeField != null)
            {
                var rawStart = _campaignStartTimeField.value?.Trim();
                if (!string.IsNullOrWhiteSpace(rawStart))
                {
                    var acceptedFormats = new[]
                    {
                        "yyyy-MM-dd HH:mm",
                        "yyyy-MM-ddTHH:mm",
                        "yyyy-MM-ddTHH:mm:ss",
                        "yyyy-MM-dd HH:mm:ss",
                        "o"
                    };

                    if (!DateTime.TryParseExact(
                            rawStart,
                            acceptedFormats,
                            CultureInfo.InvariantCulture,
                            DateTimeStyles.AssumeLocal,
                            out var parsedStart) &&
                        !DateTime.TryParse(rawStart, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out parsedStart))
                    {
                        if (_settingsStatusLabel != null)
                            _settingsStatusLabel.text = "Start time must be a valid date/time.";
                        Debug.LogWarning($"Invalid CampaignTemplate start time: '{rawStart}'");
                        return false;
                    }

                    campaign.CampaignStartTime = parsedStart;
                }
            }

            campaign.SimulationSettings ??= new SimulationSettings();
            if (_simulationTickMinutesField != null)
                campaign.SimulationSettings.SimulationTickMinutes = _simulationTickMinutesField.value;

            if (_operationalCadenceHoursField != null)
                campaign.SimulationSettings.OperationalCadenceHours = _operationalCadenceHoursField.value;

            campaign.SimulationSettings.Normalize();
            _simulationTickMinutesField?.SetValueWithoutNotify(campaign.SimulationSettings.SimulationTickMinutes);
            _operationalCadenceHoursField?.SetValueWithoutNotify(campaign.SimulationSettings.OperationalCadenceHours);

            if (_settingsStatusLabel != null)
                _settingsStatusLabel.text = "Settings ready.";

            return true;
        }
    }
}

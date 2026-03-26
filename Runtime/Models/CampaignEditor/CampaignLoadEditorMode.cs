using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.UIElements;
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
                
                var sortedFiles = files.OrderByDescending(f => File.GetLastWriteTime(f)).ToList();
                _filePaths.AddRange(sortedFiles);

                _listView?.RefreshItems(); 
            }
            catch (Exception e)
            {
                Debug.LogError($"[CampaignLoad] Failed to refresh list: {e.Message}");
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

            Debug.Log($"Loading Campaign: {Path.GetFileName(_pendingLoadPath)}");
            _editor.LoadCampaignFromJson(_pendingLoadPath);
            
            HideLoadPopup();
        }

        // Save popup methods
        private void ShowSavePopup()
        {
            if (_popupOverlay == null || _saveNamePopup == null) return;
            
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
                Debug.LogWarning("Campaign name cannot be empty!");
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
                _editor.CaptureReferenceImageIntoCampaign();
                CampaignSavingService.SaveCampaign(Editor.editingCampaign, fullPath);
                Debug.Log($"Campaign saved: {campaignName}");
                RefreshList();
                HideSavePopup();
            }
            catch (Exception e)
            {
                Debug.LogError($"Failed to save campaign: {e.Message}");
            }
        }
    }
}

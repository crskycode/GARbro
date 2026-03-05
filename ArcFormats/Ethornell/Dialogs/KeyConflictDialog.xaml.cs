using System;
using System.Collections.Generic;
using System.Windows;

namespace GameRes.Formats.BGI
{
    /// <summary>
    /// Interaction logic for KeyConflictDialog.xaml
    /// </summary>
    public partial class KeyConflictDialog : Window
    {
        public Dictionary<string, uint> ResolvedKeys { get; private set; }

        private Queue<(string filename, uint oldKey, uint newKey)> _remainingConflicts;
        private Dictionary<string, uint> _existingKeys;
        private string _currentAction;

        public KeyConflictDialog(List<(string filename, uint oldKey, uint newKey)> conflicts,
                                Dictionary<string, uint> existingKeys)
        {
            InitializeComponent();

            _existingKeys = new Dictionary<string, uint>(existingKeys);
            _remainingConflicts = new Queue<(string, uint, uint)>(conflicts);

            ShowNextConflict();
        }

        private void ShowNextConflict()
        {
            if (_remainingConflicts.Count == 0)
            {
                // All conflicts resolved
                DialogResult = true;
                Close();
                return;
            }

            var conflict = _remainingConflicts.Peek();
            FileNameRun.Text = conflict.filename;
            OldKeyText.Text = $"0x{conflict.oldKey:X8}";
            NewKeyText.Text = $"0x{conflict.newKey:X8}";

            // Update title with count
            Title = $"BGI Key Conflict ({_remainingConflicts.Count} remaining)";
        }

        private void ProcessCurrentConflict(string action)
        {
            if (ResolvedKeys == null)
                ResolvedKeys = new Dictionary<string, uint>(_existingKeys);

            var conflict = _remainingConflicts.Dequeue();

            if (action == "Keep New Key")
            {
                ResolvedKeys[conflict.filename] = conflict.newKey;
            }
            else
            {
                ResolvedKeys[conflict.filename] = conflict.oldKey;
            }

            // If "Apply to all" is checked, process all remaining conflicts with the same action
            if (ApplyToAll.IsChecked == true)
            {
                while (_remainingConflicts.Count > 0)
                {
                    var nextConflict = _remainingConflicts.Dequeue();

                    if (action == "Keep New Key")
                    {
                        ResolvedKeys[nextConflict.filename] = nextConflict.newKey;
                    }
                    else
                    {
                        ResolvedKeys[nextConflict.filename] = nextConflict.oldKey;
                    }
                }
                DialogResult = true;
                Close();
            }
            else
            {
                ShowNextConflict();
            }
        }

        private void KeepNew_Click(object sender, RoutedEventArgs e)
        {
            ProcessCurrentConflict("Keep New Key");
        }

        private void KeepOld_Click(object sender, RoutedEventArgs e)
        {
            ProcessCurrentConflict("Keep Old Key");
        }

        private void Cancel_Click(object sender, RoutedEventArgs e)
        {
            ResolvedKeys = null;
            DialogResult = false;
            Close();
        }
    }
}
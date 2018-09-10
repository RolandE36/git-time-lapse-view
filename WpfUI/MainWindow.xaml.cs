﻿using ICSharpCode.AvalonEdit.Highlighting;
using ICSharpCode.AvalonEdit.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Animation;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using TimeLapseView;
using ICSharpCode.AvalonEdit.Document;
using Microsoft.Win32;
using WpfUI.Renderer;
using System.Threading;

namespace WpfUI {
	/// <summary>
	/// Interaction logic for MainWindow.xaml
	/// </summary>
	public partial class MainWindow : Window {

		private const string APP_TITLE = "Git Time Lapse View";

		private FileHistoryManager manager;
		private CanvasTreeRenderer treeRenderer;
		private int page = 0;

		public MainWindow() {
			InitializeComponent();
		}

		ViewData View;

		class MovieData {
			public string Title { get; set; }
			public string ImageData { get; set; }
		}

		protected override void OnInitialized(EventArgs e) {
			base.OnInitialized(e);
			// TODO: Spaces.......
			lblCommitMessageLabel.Text = "\nMessage ";
			lblFilePathLabel.Text = "\nFile         ";
		}

		private void slHistoyValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e) {
			if (View == null) return;
			View.SelectSnapshot((int)(slHistoy.Maximum - slHistoy.Value));
		}

		private void lvVerticalHistoryPanel_SelectionChanged(object sender, SelectionChangedEventArgs e) {
			View.SelectSnapshot(lvVerticalHistoryPanel.SelectedIndex);
		}


		private void btnBrowseFile_Click(object sender, RoutedEventArgs e) {
			OpenFileDialog openFileDialog = new OpenFileDialog();
			openFileDialog.Multiselect = false;
			openFileDialog.RestoreDirectory = true;
			if (openFileDialog.ShowDialog() == true) {
				try {
					var filename = openFileDialog.FileNames.FirstOrDefault();

					manager = new FileHistoryManager(filename);
					Title = APP_TITLE + ": " + manager.filePath;
					View = new ViewData();

					manager.OnSnapshotsHistoryUpdated = (snapshots) => {
						this.Dispatcher.BeginInvoke(new Action(() => {
							if (snapshots.Count() == 0) return;

							//View = new ViewData();
							View.Snapshots = snapshots;
							slHistoy.Maximum = View.Snapshots.Count;
							slHistoy.Value = View.Snapshots.Count - View.Snapshot.ViewIndex;
							tbCode.Text = View.Snapshot.File;
							slHistoy.Minimum = 1;
							SetBackgroundRendererMode(RendererMode.TimeLapse);
							lblCommitDetailsSection.Visibility = Visibility.Visible;

							Canvas1.Children.Clear();
							var crt = new CanvasTreeRenderer(View, snapshots, Canvas1);

							crt.BuildTree();
							crt.Draw();

							treeRenderer = crt;
						}));
					};
					
					

					new Thread(() => {
						while (page < 5) {
							manager.GetCommitsHistory(page);
							page++;
						}
					}
					).Start();
					// TODO: Stop ).Start();



					// TODO: Mediator patern????
					// TODO: View should exist without snapshots
					View.OnViewIndexChanged = (index, snapshot) => {
						this.Dispatcher.BeginInvoke(new Action(() => {
							tbCode.Text = snapshot.File;
							slHistoy.Value = slHistoy.Maximum - index;
							lvVerticalHistoryPanel.SelectedIndex = index;
							UpdateCommitDetails(snapshot);
						}));
					};

					View.OnSelectionChanged = () => {
						this.Dispatcher.BeginInvoke(new Action(() => {
							treeRenderer.ClearHighlighting();
							treeRenderer.Draw();
						}));
					};

					// TODO: Implement Search by commits
					// TODO: Highlight code on hover
					lvVerticalHistoryPanel.ItemsSource = View.Snapshots;
				} catch (OutOfMemoryException ex) {
					// TODO: "Try to change end date." - add abilty to choose end dates or commits count.
					MessageBox.Show("File history too large.");
				} catch (Exception ex) {
					MessageBox.Show("Oops! Something went wrong.");
				}
			}
		}

		/// <summary>
		/// Prevent scrolling after keyboard events
		/// </summary>
		/// <param name="sender"></param>
		/// <param name="e"></param>
		private void CanvasScrollViewer_PreviewKeyDown(object sender, KeyEventArgs e) {
			e.Handled = true;
		}

		private void Canvas_MouseDown(object sender, MouseButtonEventArgs e) {
			Keyboard.ClearFocus();
			Canvas1.Focus();
		}

		private void MainWindow_KeyUp(object sender, KeyEventArgs e) {
			switch (e.Key) {
				case Key.Down: View.MoveToNextSnapshot(); break;
				case Key.Up: View.MoveToPrevSnapshot(); break;
				case Key.Left: View.MoveToLeftSnapshot(); break;
				case Key.Right: View.MoveToRightSnapshot(); break;
			}

			e.Handled = true;
		}

		private void btnTimeLapseViewMode_Click(object sender, RoutedEventArgs e) {
			SetBackgroundRendererMode(RendererMode.TimeLapse);
		}

		private void btnIncrementalViewMode_Click(object sender, RoutedEventArgs e) {
			SetBackgroundRendererMode(RendererMode.IncrementalDiff);
		}

		private void btnExit_Click(object sender, RoutedEventArgs e) {
			Close();
		}

		private void tbCode_MouseDoubleClick(object sender, MouseButtonEventArgs e) {
			try {
				if (View.SelectedSnapshotIndex == View.Snapshot.GetLineBirth(tbCode.TextArea.Caret.Line - 1)) {

					View.ResetSnapshotsSelection();

					View.SelectedSnapshotIndex = -1;
					View.SelectedLine = -1;
					View.SelectedLineLID = -1;
					int index = (int)(slHistoy.Maximum - slHistoy.Value);

					UpdateCommitDetails(View.Snapshot);

					slHistoy.IsSelectionRangeEnabled = false;
				} else {
					View.SelectedSnapshotIndex = View.Snapshot.GetLineBirth(tbCode.TextArea.Caret.Line - 1);
					View.SelectedLineLID = View.Snapshot.FileDetails[tbCode.TextArea.Caret.Line - 1].LID;
					View.SelectedLine = tbCode.TextArea.Caret.Line - 1;

					View.SelectSnapshots(CodeFile.LineBase[View.Snapshot.FileDetails.LineHistory[View.SelectedLine]]);
					UpdateCommitDetails(View.Snapshots[View.SelectedSnapshotIndex]);

					slHistoy.IsSelectionRangeEnabled = true;
					slHistoy.SelectionStart = slHistoy.Maximum - View.Snapshot.GetLineBirth(tbCode.TextArea.Caret.Line - 1);
					slHistoy.SelectionEnd = slHistoy.Maximum - View.Snapshot.GetLineDeath(tbCode.TextArea.Caret.Line - 1);
				}

				tbCode.TextArea.TextView.Redraw();
			} catch (Exception ex) {

			}
		}

		/// <summary>
		/// Choose methout for code background highlighting
		/// </summary>
		/// <param name="mode">Rendering mode</param>
		private void SetBackgroundRendererMode(RendererMode mode) {
			tbCode.TextArea.TextView.BackgroundRenderers.Clear();
			switch (mode) {
				case RendererMode.TimeLapse:
					tbCode.TextArea.TextView.BackgroundRenderers.Add(new TimeLapseLineBackgroundRenderer(View));
					break;
				case RendererMode.IncrementalDiff:
					tbCode.TextArea.TextView.BackgroundRenderers.Add(new IncrementalDiffLineBackgroundRenderer(View));
					break;
			}
			
			tbCode.TextArea.TextView.BackgroundRenderers.Add(new SelectedLineBackgroundRenderer(View));
			tbCode.TextArea.TextView.Redraw();

			// Set Menu Check Boxes
			menuTimeLapseViewMode.IsChecked = false;
			menuIncrementalViewMode.IsChecked = false;
			switch (mode) {
				case RendererMode.TimeLapse:
					menuTimeLapseViewMode.IsChecked = true;
					break;
				case RendererMode.IncrementalDiff:
					menuIncrementalViewMode.IsChecked = true;
					break;
			}
		}

		/// <summary>
		/// Show information about commit
		/// </summary>
		/// <param name="commit">Selected commit</param>
		private void UpdateCommitDetails(Snapshot snapshot) {
			var commit                = snapshot.Commit;
			lblCommitSha.Text         = commit.Sha;
			lblCommitAuthor.Text      = commit.Author;
			lblCommitDate.Text        = commit.Date.ToString();
			lblCommitMessageText.Text = commit.Description;
			lblFilePath.Text          = snapshot.FilePath;
			// TODO: Multiline description not working
		}

		/// <summary>
		/// Go to the next file diff
		/// </summary>
		private void menuNextDiff_Click(object sender, RoutedEventArgs e) {
			var lines = View.Snapshot.FileDetails;
			var i = tbCode.TextArea.Caret.Line;
			if (i >= lines.Count) i = lines.Count - 1;

			while (i < lines.Count && lines[i].State != LineState.Unchanged) i++; // Skip current diff
			while (i < lines.Count && lines[i].State == LineState.Unchanged) i++; // Find next diff
			i++;

			tbCode.ScrollTo(i, 0);
			tbCode.TextArea.Caret.Line = i;
			tbCode.TextArea.Caret.Column = 1;
			tbCode.TextArea.Focus();
		}

		/// <summary>
		/// Go to the previous file diff
		/// </summary>
		private void menuPrevDiff_Click(object sender, RoutedEventArgs e) {
			var lines = View.Snapshot.FileDetails;
			var i = tbCode.TextArea.Caret.Line - 1;
			if (i <= 0) i = 0;

			while (i > 0 && lines[i].State != LineState.Unchanged) i--; // Skip current diff
			while (i > 0 && lines[i].State == LineState.Unchanged) i--; // Find next diff
			i++;

			tbCode.ScrollTo(i, 0);
			tbCode.TextArea.Caret.Line = i;
			tbCode.TextArea.Caret.Column = 1;
			tbCode.TextArea.Focus();
		}
	}
}

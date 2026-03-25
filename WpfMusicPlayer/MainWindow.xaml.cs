using System.Collections.ObjectModel;
using System.IO;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.Windows.Threading;
using Microsoft.Win32;
using NAudio.Wave;
using static System.Windows.Forms.LinkLabel;

namespace WpfMusicPlayer
{

    public partial class MainWindow : Window
    {
        private List<string> Tracks_path;

        private WaveOutEvent outputDevice;
        private AudioFileReader audioFile;

        private DispatcherTimer timer;

        private bool isDragging = false;
        private bool isPlaying = false;
        private bool isReplaying = false;
        private bool isShuffling = false;

        private int currentTrackIndex = -1;
        public MainWindow()
        {
            InitializeComponent();
            Tracks_path = new List<string>();
            timer = new DispatcherTimer();
            timer.Interval = TimeSpan.FromSeconds(1);
            timer.Tick += Timer_Tick;


            Load_Tracks();
        }

        private void btnAdd_Click(object sender, RoutedEventArgs e)
        {
            OpenFileDialog files = new OpenFileDialog
            {
                Multiselect = true,
                Filter = "Audio Files|*.mp3;*.wav;*.wma"
            };
            if (files.ShowDialog() == true)
            {
                foreach (string file in files.FileNames)
                {
                    Tracks_path.Add(file);
                    TrackBoxList.Items.Add(System.IO.Path.GetFileNameWithoutExtension(file));
                }
            }

        }
        private void btnPlayStop_CLick(object sender, RoutedEventArgs e)
        {
            if (TrackBoxList.SelectedIndex == -1)
            {
                MessageBox.Show("Choose Track");
                return;
            }

            int selectedIndex = TrackBoxList.SelectedIndex;

            if (!isPlaying)
            {
                if (selectedIndex != currentTrackIndex || outputDevice == null || audioFile == null)
                {
                    PlayNewTrack(selectedIndex);
                }
                else
                {
                    outputDevice.Play();
                    timer?.Start();
                    isPlaying = true;
                }

                btnPlayStop.Content = "⏹";
            }
            else
            {
                outputDevice?.Pause();
                timer?.Stop();
                isPlaying = false;
                btnPlayStop.Content = "⏵";
            }
        }

        private void PlayNewTrack(int index)
        {
            if (outputDevice != null)
            {
                outputDevice.Stop();
                outputDevice.Dispose();
                outputDevice = null;
            }
            if (audioFile != null)
            {
                audioFile.Dispose();
                audioFile = null;
            }

            string selectedTrack = Tracks_path[index];
            currentTrackIndex = index;

            outputDevice = new WaveOutEvent();
            audioFile = new AudioFileReader(selectedTrack);
            audioFile.Volume = (float)VolumeSlider.Value;

            outputDevice.Init(audioFile);
            outputDevice.Play();
            timer?.Start();

            isPlaying = true;
        }


        private void Timer_Tick(object sender, EventArgs e)
        {
            if (outputDevice != null)
            {
                TimeSpan currentTime = audioFile.CurrentTime;
                TimeSpan totalTime = audioFile.TotalTime;

                lbl_CurrentTime.Content = currentTime.ToString(@"mm\:ss");
                lbl_TotalTime.Content = totalTime.ToString(@"mm\:ss");

                TrackSlider.Maximum = totalTime.TotalSeconds;
                TrackSlider.Value = currentTime.TotalSeconds;
            }
            if (TrackSlider.Value >= TrackSlider.Maximum - 1)
            {
                NextTrack();
                PlayNewTrack(currentTrackIndex);
            }
        }
        private void btnReplay_Click(object sender, RoutedEventArgs e)
        {
            isReplaying = !isReplaying;
            if(isReplaying)
                btnReplay.Foreground = Brushes.LimeGreen;
            else
                btnReplay.Foreground = Brushes.White;

        }
        private void btnShuffle_Click(object sender, RoutedEventArgs e)
        {
            isShuffling = !isShuffling;
            if (isShuffling)
                btnShuffle.Foreground = Brushes.LimeGreen;
            else
                btnShuffle.Foreground = Brushes.White;

        }
        private void NextTrack()
        {
            if (currentTrackIndex >= Tracks_path.Count-1 && !isReplaying)
                currentTrackIndex = 0;
            if(!isReplaying)
                currentTrackIndex++;
            if(isShuffling && !isReplaying)
                currentTrackIndex = new Random().Next(0, Tracks_path.Count);


        }

        private void TrackSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {

            if (isDragging && audioFile != null)
            {
                audioFile.CurrentTime = TimeSpan.FromSeconds(TrackSlider.Value);
            }
        }
        private void TrackSlider_PreviewMouseDown(object sender, MouseButtonEventArgs e)
        {
            isDragging = true;
        }

        private void TrackSlider_PreviewMouseUp(object sender, MouseButtonEventArgs e)
        {
            isDragging = false;
            audioFile.CurrentTime = TimeSpan.FromSeconds(TrackSlider.Value);
        }

        private void TrackSlider_PreviewMouseLeftButtonDown(object sender, MouseButtonEventArgs e)
        {
            if (audioFile == null) return;

            Slider slider = (Slider)sender;
            Point clickPosition = e.GetPosition(slider);
            double ratio = clickPosition.X / slider.ActualWidth;

            double newTime = slider.Minimum + (slider.Maximum - slider.Minimum) * ratio;

            audioFile.CurrentTime = TimeSpan.FromSeconds(newTime);
            slider.Value = newTime;

            e.Handled = true;
        }

        private void VolumeSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
        {
            if (audioFile != null)
            {
                audioFile.Volume = (float)VolumeSlider.Value;
            }
        }

        private void TextBox_TextChanged(object sender, TextChangedEventArgs e)
        {
            string searchText = TextBSearch.Text.Trim().ToLower();

            SearchedTrackBList.Items.Clear();
            if (string.IsNullOrEmpty(TextBSearch.Text))
                SearchPlaceholder.Visibility = Visibility.Visible;
            else
                SearchPlaceholder.Visibility = Visibility.Collapsed;
            if(string.IsNullOrWhiteSpace(searchText))
                return;

            foreach (var track in Tracks_path)
            {
                if (track.ToLower().Contains(searchText))
                    SearchedTrackBList.Items.Add(System.IO.Path.GetFileNameWithoutExtension(track));
            }
        }

        protected override void OnClosed(System.EventArgs e)
        {
            base.OnClosed(e);
            if (outputDevice != null)
            {
                outputDevice.Stop();
                outputDevice.Dispose();
                outputDevice = null;
            }
            if (audioFile != null)
            {
                audioFile.Dispose();
                audioFile = null;
            }
            Save_Tracks();
        }

        private void Save_Tracks()
        {
            string file_path = "Track.txt";
            File.WriteAllLines(file_path, Tracks_path);
        }
        private void Load_Tracks()
        {
            string file_path = "Track.txt";
            if (File.Exists(file_path))
            {
                string[] lines = File.ReadAllLines(file_path);
                foreach (var path in lines)
                {
                    if(File.Exists(path))
                    { 
                        Tracks_path.Add(path);
                        TrackBoxList.Items.Add(System.IO.Path.GetFileNameWithoutExtension(path));
                    }
                }
            }
        }
    }
}
using Microsoft.Win32;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using System;
using System.Collections.Generic;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using static System.Windows.Forms;

#nullable disable

namespace StoryToSceneImages
{
    // ------------------------------
    // Simple DTO for project saving
    // ------------------------------
    public class ProjectData
    {
        public string Story { get; set; } = "";
        public int SceneCount { get; set; } = 6;
        public bool CharacterConsistency { get; set; } = true;
        public bool StyleConsistency { get; set; } = true;
        public bool AutoSceneHints { get; set; } = true;
        public string ReferenceImagePath { get; set; }
        public string OutputFolder { get; set; }
    }

    public partial class MainWindow : Window
    {
        // =========================================================
        //  API KEYS – EDIT THESE STRINGS
        // =========================================================
        //  Image (free)
        private string DeepAiApiKey = "b503e042-53cc-482b-9f19-3e6e17eef6c9";

        //  Image (paid / optional)
        private string HuggingFaceApiKey = "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";
        private string LeonardoApiKey = "YOUR_LEONARDO_KEY";

        //  Video (free = Pixlr uses browser, no key needed)
        //  Video (paid = HeyGen)
        private string HeyGenApiKey = "sk_V2_hgu_k1uusedBLrj_uMqhSv7rv0w3IyKkY39GUsPyjtOAxmux";

        //  HuggingFace model & endpoint (image)
        private const string HuggingFaceModelName =
            "black-forest-labs/FLUX.1-dev";

        private static readonly string HuggingFaceEndpoint =
            $"https://router.huggingface.co/hf-inference/models/{HuggingFaceModelName}";

        // DeepAI endpoint
        private const string DeepAiEndpoint =
            "https://api.deepai.org/api/text2img";

        // Leonardo endpoint (stubbed)
        private const string LeonardoEndpoint =
            "https://cloud.leonardo.ai/api/rest/v1/generations";

        // HeyGen endpoint (simple text-to-video call)
        private const string HeyGenEndpoint =
            "https://api.heygen.com/v2/video/generate";

        // Pixlr – we just open their web page
        private const string PixlrVideoPageUrl =
            "https://pixlr.com/video/";

        // =========================================================
        //  RUNTIME STATE
        // =========================================================
        private readonly HttpClient _httpClient = new HttpClient();
        private readonly List<BitmapSource> _generatedImages = new List<BitmapSource>();
        private readonly List<string> _generatedImagePaths = new List<string>();

        private BitmapImage _referenceImage;
        private string _referenceImagePath;
        private string _outputFolder;

        public string Description { get; private set; }

        public MainWindow()
        {
            InitializeComponent();

            // Defaults
            Title = "Story to Scene Images";
            StatusTextBlock.Text = "Ready.";

            // Default image size
            if (ImageSizeComboBox.Items.Count > 0)
                ImageSizeComboBox.SelectedIndex = 0;

            // Default engines
            if (ImageEngineComboBox.Items.Count > 0)
                ImageEngineComboBox.SelectedIndex = 0;
            if (VideoEngineComboBox.Items.Count > 0)
                VideoEngineComboBox.SelectedIndex = 0;

            ImageModeFreeRadio.IsChecked = true;
            VideoModeFreeRadio.IsChecked = true;

            // Default scenes
            ScenesCountTextBox.Text = "6";
        }

        // =========================================================
        //  BUTTON HANDLERS
        // =========================================================

        private async void GenerateButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await GenerateScenesAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show(
                    "Error while generating images:\n\n" + ex.Message,
                    "Error",
                    MessageBoxButton.OK,
                    MessageBoxImage.Error);
                StatusTextBlock.Text = "Generation failed.";
                ToggleUi(true);
            }
        }

        private void SaveImagesButton_Click(object sender, RoutedEventArgs e)
        {
            if (_generatedImages.Count == 0)
            {
                MessageBox.Show("No images to save yet.");
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Choose folder & base name",
                FileName = "scene_1.png",
                Filter = "PNG Image|*.png"
            };

            if (dialog.ShowDialog() == true)
            {
                string folder = Path.GetDirectoryName(dialog.FileName);
                for (int i = 0; i < _generatedImages.Count; i++)
                {
                    string path = Path.Combine(folder, $"scene_{i + 1}.png");
                    SaveBitmapToPng(_generatedImages[i], path);
                }

                MessageBox.Show($"Saved {_generatedImages.Count} images.");
            }
        }

        private void ExportZipButton_Click(object sender, RoutedEventArgs e)
        {
            if (_generatedImages.Count == 0)
            {
                MessageBox.Show("No images to export yet.");
                return;
            }

            var dialog = new SaveFileDialog
            {
                Title = "Export ZIP",
                FileName = "scenes.zip",
                Filter = "ZIP Archive|*.zip"
            };

            if (dialog.ShowDialog() == true)
            {
                ExportImagesToZip(dialog.FileName);
                MessageBox.Show("ZIP exported.");
            }
        }

        private void SaveProjectButton_Click(object sender, RoutedEventArgs e)
        {
            var project = new ProjectData
            {
                Story = StoryTextBox.Text ?? "",
                SceneCount = int.TryParse(ScenesCountTextBox.Text, out int n) ? n : 6,
                CharacterConsistency = CharacterConsistencyCheckBox.IsChecked == true,
                StyleConsistency = StyleConsistencyCheckBox.IsChecked == true,
                AutoSceneHints = AddSceneHintsCheckBox.IsChecked == true,
                ReferenceImagePath = _referenceImagePath,
                OutputFolder = _outputFolder
            };

            var dialog = new SaveFileDialog
            {
                Title = "Save Project",
                FileName = "story_project.json",
                Filter = "Project File (*.json)|*.json"
            };

            if (dialog.ShowDialog() == true)
            {
                // FIX: Use Newtonsoft.Json.Formatting.Indented as the second argument
                string json = Newtonsoft.Json.JsonConvert.SerializeObject(project, Newtonsoft.Json.Formatting.Indented);

                File.WriteAllText(dialog.FileName, json, Encoding.UTF8);
                StatusTextBlock.Text = "Project saved.";
            }
        }

        private void LoadProjectButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Load Project",
                Filter = "Project File (*.json)|*.json"
            };

            if (dialog.ShowDialog() == true)
            {
                string json = File.ReadAllText(dialog.FileName, Encoding.UTF8);
                ProjectData project;

                try
                {
                    project = Newtonsoft.Json.JsonConvert.DeserializeObject<ProjectData>(json);
                }
                catch
                {
                    MessageBox.Show("Could not read project file.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                if (project == null)
                {
                    MessageBox.Show("Project file was empty.", "Error",
                        MessageBoxButton.OK, MessageBoxImage.Error);
                    return;
                }

                StoryTextBox.Text = project.Story;
                ScenesCountTextBox.Text = project.SceneCount.ToString();
                CharacterConsistencyCheckBox.IsChecked = project.CharacterConsistency;
                StyleConsistencyCheckBox.IsChecked = project.StyleConsistency;
                AddSceneHintsCheckBox.IsChecked = project.AutoSceneHints;

                _outputFolder = project.OutputFolder;
                if (!string.IsNullOrWhiteSpace(_outputFolder))
                    OutputFolderTextBlock.Text = _outputFolder;

                if (!string.IsNullOrWhiteSpace(project.ReferenceImagePath) &&
                    File.Exists(project.ReferenceImagePath))
                {
                    LoadReferenceImageFromPath(project.ReferenceImagePath);
                }
                else
                {
                    _referenceImage = null;
                    _referenceImagePath = null;
                    ReferencePreview.Source = null;
                    ReferenceLabel.Text = "No image selected";
                }

                StatusTextBlock.Text = "Project loaded.";
            }
        }

        private void LoadReferenceButton_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new OpenFileDialog
            {
                Title = "Choose Reference Image",
                Filter = "Image Files|*.png;*.jpg;*.jpeg;*.webp;*.bmp"
            };

            if (dialog.ShowDialog() == true)
            {
                LoadReferenceImageFromPath(dialog.FileName);
                StatusTextBlock.Text = "Reference image loaded.";
            }
        }

        private void ChooseOutputFolder_Click(object sender, RoutedEventArgs e)
        {
            var dialog = new Microsoft.Win32.OpenFileDialog
            {
                CheckFileExists = false,
                CheckPathExists = true,
                FileName = "Select Folder"
            };

            var result = dialog.ShowDialog();

            if (result == true)
            {
                string folder = System.IO.Path.GetDirectoryName(dialog.FileName);

                if (!string.IsNullOrWhiteSpace(folder))
                {
                    _outputFolder = folder;
                    OutputFolderTextBlock.Text = _outputFolder;
                }
            }
        }






        private void OpenSettings_Click(object sender, RoutedEventArgs e)
        {
            MessageBox.Show(
                "For now, API keys are edited directly in MainWindow.xaml.cs at the top of the file.\n\n" +
                "Look for the strings DeepAiApiKey, HuggingFaceApiKey, LeonardoApiKey, HeyGenApiKey.",
                "API keys",
                MessageBoxButton.OK,
                MessageBoxImage.Information);
        }

        // ---------------------------------------------------------
        //  Video generation
        // ---------------------------------------------------------
        private async void GenerateVideoButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                await GenerateVideoAsync();
            }
            catch (Exception ex)
            {
                MessageBox.Show("Video generation failed:\n\n" + ex.Message,
                    "Video Error", MessageBoxButton.OK, MessageBoxImage.Error);
                StatusTextBlock.Text = "Video generation failed.";
            }
        }

        // =========================================================
        //  CORE LOGIC – IMAGE GENERATION
        // =========================================================

        private async Task GenerateScenesAsync()
        {
            string story = (StoryTextBox.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(story))
            {
                MessageBox.Show("Please enter a story first.");
                return;
            }

            if (!int.TryParse(ScenesCountTextBox.Text, out int sceneCount) || sceneCount <= 0)
            {
                MessageBox.Show("Scenes must be a positive number.");
                return;
            }

            (int width, int height) = GetSelectedSize();

            ToggleUi(false);
            StatusTextBlock.Text = "Preparing scenes...";
            Title = "Story to Scene Images – generating...";

            _generatedImages.Clear();
            _generatedImagePaths.Clear();
            ScenesPanel.Children.Clear();

            // Decide / ensure output folder
            if (string.IsNullOrWhiteSpace(_outputFolder))
            {
                string baseFolder = Path.Combine(
                    Environment.GetFolderPath(Environment.SpecialFolder.MyPictures),
                    "StoryToSceneImages");

                Directory.CreateDirectory(baseFolder);
                _outputFolder = Path.Combine(baseFolder,
                    DateTime.Now.ToString("yyyyMMdd_HHmmss"));
                Directory.CreateDirectory(_outputFolder);
                OutputFolderTextBlock.Text = _outputFolder;
            }
            else if (!Directory.Exists(_outputFolder))
            {
                Directory.CreateDirectory(_outputFolder);
            }

            var hints = BuildSceneHints(story, sceneCount);

            try
            {
                for (int i = 0; i < sceneCount; i++)
                {
                    int sceneNumber = i + 1;
                    string sceneHint = hints[i];

                    string prompt = BuildPromptForScene(story, sceneHint, sceneNumber, sceneCount);

                    StatusTextBlock.Text =
                        $"Generating scene {sceneNumber}/{sceneCount}...";
                    await Dispatcher.InvokeAsync(() => { });

                    byte[] imgBytes = await GenerateImageForPromptAsync(prompt, width, height);

                    BitmapImage bmp = LoadImage(imgBytes);
                    _generatedImages.Add(bmp);

                    string filePath = Path.Combine(_outputFolder, $"scene_{sceneNumber}.png");
                    SaveBitmapToPng(bmp, filePath);
                    _generatedImagePaths.Add(filePath);

                    AddSceneCard(sceneNumber, bmp, sceneHint, prompt);
                }

                StatusTextBlock.Text = $"Generated {sceneCount} scenes.";
            }
            finally
            {
                ToggleUi(true);
                Title = "Story to Scene Images";
            }
        }

        private async Task<byte[]> GenerateImageForPromptAsync(string prompt, int width, int height)
        {
            // If FREE mode → always DeepAI
            if (ImageModeFreeRadio.IsChecked == true)
            {
                if (string.IsNullOrWhiteSpace(DeepAiApiKey) ||
                    DeepAiApiKey.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase))
                {
                    throw new InvalidOperationException(
                        "DeepAI API key is not set. Open MainWindow.xaml.cs and fill DeepAiApiKey.");
                }

                return await CallDeepAiAsync(prompt, width, height);
            }

            // PAID MODE – use selected engine
            string engineTag = "deepai";
            if (ImageEngineComboBox.SelectedItem is ComboBoxItem item &&
                item.Tag is string tag && !string.IsNullOrWhiteSpace(tag))
            {
                engineTag = tag.ToLowerInvariant();
            }

            switch (engineTag)
            {
                case "deepai":
                    if (string.IsNullOrWhiteSpace(DeepAiApiKey) ||
                        DeepAiApiKey.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException(
                            "DeepAI API key is not set. Open MainWindow.xaml.cs and fill DeepAiApiKey.");

                    return await CallDeepAiAsync(prompt, width, height);

                case "huggingface":
                    if (string.IsNullOrWhiteSpace(HuggingFaceApiKey) ||
                        HuggingFaceApiKey.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException(
                            "Hugging Face API key is not set. Open MainWindow.xaml.cs and fill HuggingFaceApiKey.");

                    return await CallHuggingFaceAsync(prompt, width, height);

                case "leonardo":
                    if (string.IsNullOrWhiteSpace(LeonardoApiKey) ||
                        LeonardoApiKey.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase))
                        throw new InvalidOperationException(
                            "Leonardo API key is not set. Open MainWindow.xaml.cs and fill LeonardoApiKey.");

                    // To avoid half-broken behaviour we throw a clear message.
                    throw new NotSupportedException(
                        "Leonardo API integration is not fully implemented yet in this code sample.\n\n" +
                        "Use DeepAI or HuggingFace for now, or implement Leonardo according to their docs.");

                default:
                    throw new InvalidOperationException("Unknown image engine selection.");
            }
        }

        // ------------------------- DeepAI -------------------------
        private async Task<byte[]> CallDeepAiAsync(string prompt, int width, int height)
        {
            var req = new HttpRequestMessage(HttpMethod.Post, DeepAiEndpoint);
            req.Headers.Add("api-key", DeepAiApiKey);

            var form = new MultipartFormDataContent();
            form.Add(new StringContent(prompt), "text");
            req.Content = form;

            using HttpResponseMessage resp = await _httpClient.SendAsync(req);
            string json = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
            {
                throw new Exception($"DeepAI error {resp.StatusCode}:\n{json}");
            }

            dynamic parsed = JsonConvert.DeserializeObject(json);
            string url = parsed?.output_url;
            if (string.IsNullOrWhiteSpace(url))
                throw new Exception("DeepAI did not return an output_url.\nRaw:\n" + json);

            byte[] imgBytes = await _httpClient.GetByteArrayAsync(url);
            return imgBytes;
        }

        // ---------------------- HuggingFace -----------------------
        private async Task<byte[]> CallHuggingFaceAsync(string prompt, int width, int height)
        {
            var body = new
            {
                inputs = prompt,
                parameters = new { width, height }
            };

            string json = System.Text.Json.JsonSerializer.Serialize(body);

            var req = new HttpRequestMessage(HttpMethod.Post, HuggingFaceEndpoint);
            req.Headers.Authorization =
                new AuthenticationHeaderValue("Bearer", HuggingFaceApiKey);
            req.Content = new StringContent(json, Encoding.UTF8, "application/json");

            using HttpResponseMessage resp = await _httpClient.SendAsync(req);
            if (!resp.IsSuccessStatusCode)
            {
                string errText = await resp.Content.ReadAsStringAsync();
                throw new Exception($"Hugging Face API error: {resp.StatusCode}\n\n{errText}");
            }

            return await resp.Content.ReadAsByteArrayAsync();
        }

        // =========================================================
        //  VIDEO GENERATION
        // =========================================================
        private async Task GenerateVideoAsync()
        {
            // Must have at least one image on disk
            if (_generatedImagePaths.Count == 0)
            {
                MessageBox.Show("Generate and save at least one image first.");
                return;
            }

            string prompt = (VideoPromptTextBox.Text ?? "").Trim();
            if (string.IsNullOrWhiteSpace(prompt))
                prompt = "Create a cinematic trailer using the generated scenes.";

            // FREE → Pixlr (open browser)
            if (VideoModeFreeRadio.IsChecked == true)
            {
                StatusTextBlock.Text = "Opening Pixlr video generator in your browser...";
                System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                {
                    FileName = PixlrVideoPageUrl,
                    UseShellExecute = true
                });

                // Also open the folder with images for easy upload
                if (!string.IsNullOrWhiteSpace(_outputFolder) &&
                    Directory.Exists(_outputFolder))
                {
                    System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
                    {
                        FileName = _outputFolder,
                        UseShellExecute = true
                    });
                }

                return;
            }

            // PAID → HeyGen API
            if (string.IsNullOrWhiteSpace(HeyGenApiKey) ||
                HeyGenApiKey.StartsWith("YOUR_", StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    "HeyGen API key is not set. Open MainWindow.xaml.cs and fill HeyGenApiKey.");
            }

            StatusTextBlock.Text = "Calling HeyGen video API...";

            // For simplicity we make a text-only request.
            // You can expand this to send image URLs if your HeyGen plan/API supports it.
            var payload = new
            {
                video_inputs = new[]
                {
                        new
                    {
                        type = "text",
                        content = prompt
                     }
                },
                     aspect_ratio = "16:9"
            };


            string payloadJson = System.Text.Json.JsonSerializer.Serialize(payload);


            var req = new HttpRequestMessage(HttpMethod.Post, HeyGenEndpoint);
            req.Headers.Add("X-Api-Key", HeyGenApiKey);
            req.Content = new StringContent(payloadJson, Encoding.UTF8, "application/json");

            using HttpResponseMessage resp = await _httpClient.SendAsync(req);
            string respText = await resp.Content.ReadAsStringAsync();

            if (!resp.IsSuccessStatusCode)
                throw new Exception($"HeyGen error {resp.StatusCode}:\n{respText}");

            dynamic parsed = JsonConvert.DeserializeObject(respText);
            // Many examples show `video_url`; others show `video_id`.
            string videoUrl = parsed?.data?.video_url ?? parsed?.video_url;

            if (string.IsNullOrWhiteSpace(videoUrl))
            {
                MessageBox.Show(
                    "HeyGen response did not contain a direct video URL.\n" +
                    "Raw response will be shown for debugging.",
                    "HeyGen",
                    MessageBoxButton.OK,
                    MessageBoxImage.Information);

                MessageBox.Show(respText, "HeyGen raw response");
                StatusTextBlock.Text = "HeyGen finished (inspect response).";
                return;
            }

            // Open video URL in browser
            System.Diagnostics.Process.Start(new System.Diagnostics.ProcessStartInfo
            {
                FileName = videoUrl,
                UseShellExecute = true
            });

            StatusTextBlock.Text = "Video generated (HeyGen).";
        }

        // =========================================================
        //  SUPPORT / UTILITY FUNCTIONS
        // =========================================================

        private void LoadReferenceImageFromPath(string path)
        {
            var img = new BitmapImage();
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read))
            {
                img.BeginInit();
                img.CacheOption = BitmapCacheOption.OnLoad;
                img.StreamSource = fs;
                img.EndInit();
                img.Freeze();
            }

            _referenceImage = img;
            _referenceImagePath = path;
            ReferencePreview.Source = img;
            ReferenceLabel.Text = System.IO.Path.GetFileName(path);
        }

        private void ToggleUi(bool enabled)
        {
            GenerateButton.IsEnabled = enabled;
            SaveImagesButton.IsEnabled = enabled;
            ExportZipButton.IsEnabled = enabled;
            SaveProjectButton.IsEnabled = enabled;
            LoadProjectButton.IsEnabled = enabled;
            LoadReferenceButton.IsEnabled = enabled;
            ChooseOutputFolderButton.IsEnabled = enabled;
            GenerateVideoButton.IsEnabled = enabled;
        }

        private (int width, int height) GetSelectedSize()
        {
            string selected = (ImageSizeComboBox.SelectedItem as ComboBoxItem)?.Content as string;

            if (selected != null)
            {
                if (selected.Contains("1280x720"))
                    return (1280, 720);
                if (selected.Contains("768x768"))
                    return (768, 768);
            }
            return (512, 512);
        }

        private List<string> BuildSceneHints(string story, int sceneCount)
        {
            var hints = new List<string>();

            string[] defaultLabels =
            {
                "Opening shot – introduce main character and setting",
                "Rising tension – the danger or mystery becomes clear",
                "First turning point – commitment to the journey",
                "Midpoint – serious obstacle or setback",
                "Climax – most intense and dangerous moment",
                "Resolution – aftermath and quiet closing scene"
            };

            var sentences = story
                .Split(new[] { '.', '!', '?' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(s => s.Trim())
                .Where(s => s.Length > 0)
                .ToList();

            int perScene = Math.Max(1, sentences.Count / sceneCount);

            for (int i = 0; i < sceneCount; i++)
            {
                var seg = sentences.Skip(i * perScene).Take(perScene).ToList();
                string segText = seg.Count > 0 ? string.Join(". ", seg) : story;

                string label = segText;

                if (AddSceneHintsCheckBox.IsChecked == true)
                {
                    string prefix = defaultLabels[Math.Min(i, defaultLabels.Length - 1)];
                    label = prefix + " – " + segText;
                }

                hints.Add(Truncate(label, 260));
            }

            return hints;
        }

        private string BuildPromptForScene(string fullStory, string sceneHint, int sceneNumber, int totalScenes)
        {
            var sb = new StringBuilder();

            sb.AppendLine($"Storyboard scene {sceneNumber} of {totalScenes} from a cinematic adventure.");
            sb.AppendLine("Natural colors, atmospheric lighting, detailed environment.");

            if (CharacterConsistencyCheckBox.IsChecked == true)
            {
                sb.AppendLine("Keep main characters' faces, hair, clothing and age consistent from scene to scene.");
            }

            if (StyleConsistencyCheckBox.IsChecked == true)
            {
                sb.AppendLine("Use one coherent illustrative style for all scenes.");
            }

            if (_referenceImage != null)
            {
                sb.AppendLine("Match the overall style, color palette and composition of the reference concept image visible in the app.");
            }

            sb.AppendLine();
            sb.AppendLine("Story context:");
            sb.AppendLine(fullStory);
            sb.AppendLine();
            sb.AppendLine("Focus for this specific scene:");
            sb.AppendLine(sceneHint);
            sb.AppendLine();
            sb.AppendLine("High resolution, film concept art, 4k, sharp, highly detailed.");

            return sb.ToString();
        }

        private BitmapImage LoadImage(byte[] data)
        {
            var bmp = new BitmapImage();
            using (var ms = new MemoryStream(data))
            {
                bmp.BeginInit();
                bmp.CacheOption = BitmapCacheOption.OnLoad;
                bmp.StreamSource = ms;
                bmp.EndInit();
                bmp.Freeze();
            }
            return bmp;
        }

        private void AddSceneCard(int sceneNumber, BitmapImage image, string sceneHint, string fullPrompt)
        {
            var border = new Border
            {
                Width = 310,
                Margin = new Thickness(6),
                CornerRadius = new CornerRadius(8),
                BorderThickness = new Thickness(1),
                BorderBrush = new SolidColorBrush(Color.FromRgb(55, 65, 81)),
                Background = new SolidColorBrush(Color.FromRgb(15, 23, 42)),
                Padding = new Thickness(8)
            };

            var stack = new StackPanel();

            var title = new TextBlock
            {
                Text = $"Scene {sceneNumber}",
                FontWeight = FontWeights.SemiBold,
                Foreground = Brushes.White,
                Margin = new Thickness(0, 0, 0, 2)
            };

            var hint = new TextBlock
            {
                Text = Truncate(sceneHint, 160),
                Foreground = new SolidColorBrush(Color.FromRgb(156, 163, 175)),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 11,
                Margin = new Thickness(0, 0, 0, 4)
            };

            var img = new Image
            {
                Source = image,
                Height = 180,
                Stretch = Stretch.UniformToFill,
                Margin = new Thickness(0, 0, 0, 4),
                SnapsToDevicePixels = true
            };

            var promptBlock = new TextBlock
            {
                Text = Truncate(fullPrompt.Replace(Environment.NewLine, " "), 180),
                Foreground = new SolidColorBrush(Color.FromRgb(107, 114, 128)),
                TextWrapping = TextWrapping.Wrap,
                FontSize = 10
            };

            stack.Children.Add(title);
            stack.Children.Add(hint);
            stack.Children.Add(img);
            stack.Children.Add(promptBlock);

            border.Child = stack;
            ScenesPanel.Children.Add(border);
        }

        private void ExportImagesToZip(string zipPath)
        {
            if (File.Exists(zipPath))
                File.Delete(zipPath);

            using var zipStream = new FileStream(zipPath, FileMode.Create);
            using var archive = new ZipArchive(zipStream, ZipArchiveMode.Create);

            for (int i = 0; i < _generatedImages.Count; i++)
            {
                string entryName = $"scene_{i + 1}.png";
                var entry = archive.CreateEntry(entryName, CompressionLevel.Optimal);

                using var entryStream = entry.Open();
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(_generatedImages[i]));
                encoder.Save(entryStream);
            }
        }

        private void SaveBitmapToPng(BitmapSource bitmap, string path)
        {
            var encoder = new PngBitmapEncoder();
            encoder.Frames.Add(BitmapFrame.Create(bitmap));
            using var fs = new FileStream(path, FileMode.Create);
            encoder.Save(fs);
        }

        private string Truncate(string text, int max)
        {
            if (string.IsNullOrEmpty(text) || text.Length <= max)
                return text;
            return text.Substring(0, max) + "...";
        }
    }
}

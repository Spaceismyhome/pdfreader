using System.Text;
using System.Text.RegularExpressions;
using UglyToad.PdfPig;
using Microsoft.Maui.Media;

namespace pdfreader.Views;

public partial class ReaderPage : ContentPage
{
    private readonly string _filePath;
    private bool _isPaused = false;
    private bool _isReading = false;
    private double _speechRate = 0.1;
    private const double MinRate = 0.25;
    private const double MaxRate = 1.0;
    private int _currentPage = 1;
    private int _currentChunkIndex = 0;
    private Dictionary<int, string> _pageFullText = new Dictionary<int, string>();
    private int _totalPages = 0;
    private PdfDocument? _pdfDocument;
    private CancellationTokenSource? _cancellationTokenSource;
    private List<string> _currentPageChunks = new List<string>();
    private readonly object _lockObject = new object();
    private bool _isDisposing = false;

    public ReaderPage(string filePath)
    {
        InitializeComponent();
        _filePath = filePath;
        Title = Path.GetFileNameWithoutExtension(filePath);

        try
        {
            _pdfDocument = PdfDocument.Open(_filePath);
            _totalPages = _pdfDocument.NumberOfPages;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error opening PDF: {ex.Message}");
            MainThread.BeginInvokeOnMainThread(() =>
            {
                StatusLabel.Text = "Error loading PDF";
            });
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            PageLabel.Text = $"Page: {_currentPage}";
            StatusLabel.Text = $"Loaded {_totalPages} pages";
            SpeedLabel.Text = $"Speed: {_speechRate:F1}x";
        });
    }

    private void OnSlowClicked(object sender, EventArgs e)
    {
        _speechRate = Math.Max(MinRate, _speechRate - 0.1);
        SpeedLabel.Text = $"Speed: {_speechRate:F1}x";
    }

    private void OnFastClicked(object sender, EventArgs e)
    {
        _speechRate = Math.Min(MaxRate, _speechRate + 0.1);
        SpeedLabel.Text = $"Speed: {_speechRate:F1}x";
    }

    private async Task LoadPdfAsync()
    {
        await Task.Delay(100);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            try
            {
                PdfWebView.Source = new UrlWebViewSource
                {
                    Url = $"file:///{_filePath.Replace("\\", "/")}"
                };
            catch (Exception ex)
            {
                Console.WriteLine($"Error loading PDF viewer: {ex.Message}");
                StatusLabel.Text = "Error displaying PDF";
            }
        });
    }

    private async void OnReadResumeClicked(object sender, EventArgs e)
    {
        if (_isReading && !_isPaused)
            return;

        if (!_isReading)
        {
            string result = await DisplayPromptAsync(
                "Start Reading",
                $"Enter page number to start from (1-{_totalPages}):",
                initialValue: _currentPage.ToString(),
                keyboard: Keyboard.Numeric);

            if (!int.TryParse(result, out int startPage) || startPage < 1 || startPage > _totalPages)
            {
                await DisplayAlertAsync("Invalid", $"Please enter a valid page number between 1 and {_totalPages}.", "OK");
                return;
            }

            lock (_lockObject)
            {
                _currentPage = startPage;
                _currentChunkIndex = 0;
                _currentPageChunks.Clear();
            }

            await UpdatePdfViewerToPage(_currentPage);
        }

        lock (_lockObject)
        {
            _isPaused = false;
            _isReading = true;
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            StatusLabel.Text = $"Reading page {_currentPage}";
        });

        await ReadFromCurrentPosition();
    }

    private void OnPauseClicked(object sender, EventArgs e)
    {
        lock (_lockObject)
        {
            if (_isReading)
            {
                _isPaused = true;
                _cancellationTokenSource?.Cancel();
            }
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            StatusLabel.Text = $"Paused at page {_currentPage}";
        });
    }

    private async Task ReadFromCurrentPosition()
    {
        // Cancel any existing speech
        _cancellationTokenSource?.Cancel();

        // Create new cancellation token
        _cancellationTokenSource = new CancellationTokenSource();
        var cancellationToken = _cancellationTokenSource.Token;

        try
        {
            int currentPage;
            lock (_lockObject)
            {
                currentPage = _currentPage;
            }

            for (int pageNum = currentPage; pageNum <= _totalPages; pageNum++)
            {
                // Check if paused or cancelled
                if (cancellationToken.IsCancellationRequested)
                    break;

                lock (_lockObject)
                {
                    if (_isPaused)
                        break;
                    _currentPage = pageNum;
                }

                // Update UI
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    PageLabel.Text = $"Page: {pageNum}";
                    ReadingProgress.Progress = (double)pageNum / _totalPages;
                    StatusLabel.Text = $"Reading page {pageNum}";
                });

                // Update PDF viewer
                await UpdatePdfViewerToPage(pageNum);

                // Get page text
                string pageText;
                lock (_lockObject)
                {
                    if (!_pageFullText.ContainsKey(pageNum))
                    {
                        if (_pdfDocument != null)
                        {
                            var page = _pdfDocument.GetPage(pageNum);
                            _pageFullText[pageNum] = page.Text;
                        }
                    }
                    pageText = _pageFullText.ContainsKey(pageNum)
                        ? _pageFullText[pageNum]
                        : string.Empty;
                }

                if (!string.IsNullOrWhiteSpace(pageText))
                {
                    bool completed = await ReadPageFromPosition(pageText, pageNum, cancellationToken);
                    if (!completed)
                    }
                    pageText = _pageFullText.ContainsKey(pageNum)
                        ? _pageFullText[pageNum]
                        : string.Empty;
                }

<<<<<<<<< Temporary merge branch 1
                var pageText = _pageFullText.ContainsKey(pageNum)
                    ? _pageFullText[pageNum]
                    : string.Empty;

                if (!string.IsNullOrWhiteSpace(pageText))
                {
                    await ReadPageFromPosition(pageText, pageNum);
                }

                // Reset for next page (only if we finished this page)
                _currentChunkIndex = 0;
                _currentPageChunks.Clear();

                // Small delay between pages
                await Task.Delay(300);
            }

            // If we finished all pages
            if (!_isPaused && !_cancellationTokenSource.Token.IsCancellationRequested)
            {
                MainThread.BeginInvokeOnMainThread(() =>
=========
                if (!string.IsNullOrWhiteSpace(pageText))
                {
                    bool completed = await ReadPageFromPosition(pageText, pageNum, cancellationToken);
                    if (!completed)
                        break;
                }

                // Reset for next page
                lock (_lockObject)
                {
                    _currentChunkIndex = 0;
                    _currentPageChunks.Clear();
                }

                // Small delay between pages
                await Task.Delay(300, cancellationToken);
            }

            // Check if we completed all pages
            lock (_lockObject)
            {
                if (!_isPaused && !cancellationToken.IsCancellationRequested && !_isDisposing)
                {
                    MainThread.BeginInvokeOnMainThread(async () =>
                    {
                        _isReading = false;
                        _currentChunkIndex = 0;
                        _currentPageChunks.Clear();
                        StatusLabel.Text = "Reading complete";
                        ReadingProgress.Progress = 1.0;
                        await DisplayAlert("Complete", "Finished reading the document.", "OK");
                    });
                }
            }
        }
        catch (OperationCanceledException)
        {
            // Expected when cancelling
            Console.WriteLine("Reading cancelled");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading: {ex.Message}");
            MainThread.BeginInvokeOnMainThread(() =>
            {
                StatusLabel.Text = "Error during reading";
            });
        }
    }

    private async Task<bool> ReadPageFromPosition(string pageText, int pageNum, CancellationToken cancellationToken)
    {
        // Clean the text
        var cleanText = CleanTextForReading(pageText);

        // Split into chunks (only once per page)
        List<string> chunks;
        int startIndex;

        lock (_lockObject)
        {
            if (_currentPageChunks.Count == 0)
            {
                _currentPageChunks = SplitIntoSmartChunks(cleanText);
            }
            chunks = _currentPageChunks.ToList(); // Create a copy to avoid modification during iteration
            startIndex = _currentChunkIndex;
        }

        // Start from saved chunk index
        for (int i = startIndex; i < chunks.Count; i++)
        {
            // Check for pause or cancellation
            lock (_lockObject)
            {
                if (_isPaused || cancellationToken.IsCancellationRequested)
                {
                    _currentChunkIndex = i;
                    return false;
                }
                _currentChunkIndex = i;
            }

            var chunk = chunks[i];

            // Update highlight display
            await UpdateHighlightDisplay(chunk, pageNum);

            try
            {
                // Read the chunk
                await TextToSpeech.SpeakAsync(
                    chunk,
                    new SpeechOptions
                    {
                        Rate = (float)_speechRate
                    },
                    cancellationToken
                );
            }
            catch (OperationCanceledException)
            {
                return false;
            }

            // Small delay between chunks
            await Task.Delay(30, cancellationToken);
        }

        // Completed all chunks
        return true;
    }

    private List<string> SplitIntoSmartChunks(string text)
    {
        var chunks = new List<string>();
        if (string.IsNullOrWhiteSpace(text))
            return chunks;

        // FIRST: Fix abbreviation spacing issues BEFORE splitting
        text = FixAbbreviationSpacing(text);

        // Split by paragraphs first (most natural reading breaks)
        var paragraphs = text.Split(new[] { "\n\n", "\r\n\r\n" }, StringSplitOptions.RemoveEmptyEntries);

        foreach (var paragraph in paragraphs)
        {
            // Split paragraph into sentences, being careful with abbreviations
            var sentences = SplitParagraphIntoSentences(paragraph);

            // Group 1-2 sentences per chunk
            var currentChunk = new StringBuilder();

            foreach (var sentence in sentences)
            {
                if (currentChunk.Length > 0 && currentChunk.Length + sentence.Length > 250)
                {
                    chunks.Add(currentChunk.ToString().Trim());
                    currentChunk.Clear();
                }

                if (currentChunk.Length > 0)
                    currentChunk.Append(" ");

                currentChunk.Append(sentence);
            }

            if (currentChunk.Length > 0)
            {
                chunks.Add(currentChunk.ToString().Trim());
            }
        }

        return chunks;
    }

    private string FixAbbreviationSpacing(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        // Fix common abbreviation patterns
        text = Regex.Replace(text, @"\b(Mr|Mrs|Ms|Dr|Prof|St|Jr|Sr)\s*\.", "$1.", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\b(Inc|Ltd|Co|Corp|eg|ie|etc|vs)\s*\.", "$1.", RegexOptions.IgnoreCase);
        text = Regex.Replace(text, @"\b([A-Z])\s*\.\s*([A-Z])\s*\.", "$1.$2.");
        text = Regex.Replace(text, @"\b([A-Z])\s*\.", "$1.");

        return text;
    }

    private List<string> SplitParagraphIntoSentences(string paragraph)
    {
        var sentences = new List<string>();
        if (string.IsNullOrWhiteSpace(paragraph))
            return sentences;

        int start = 0;
        int position = 0;

        while (position < paragraph.Length)
        {
            char c = paragraph[position];

            if (c == '.' || c == '!' || c == '?')
            {
                bool isSentenceEnd = IsTrueSentenceEnd(paragraph, position);

                if (isSentenceEnd)
                {
                    string sentence = paragraph.Substring(start, position - start + 1).Trim();
                    if (!string.IsNullOrWhiteSpace(sentence))
                    {
                        sentences.Add(sentence);
                    }

                    start = position + 1;
                    while (start < paragraph.Length && char.IsWhiteSpace(paragraph[start]))
                    {
                        start++;
                    }

                    position = start;
                }
                else
                {
                    position++;
                }
            }
            else
            {
                position++;
            }
        }

        if (start < paragraph.Length)
        {
            string remaining = paragraph.Substring(start).Trim();
            if (!string.IsNullOrWhiteSpace(remaining))
            {
                sentences.Add(remaining);
            }
        }

        return sentences;
    }

    private bool IsTrueSentenceEnd(string text, int position)
    {
        if (position >= text.Length - 1)
            return true;

        int nextPos = position + 1;
        while (nextPos < text.Length && char.IsWhiteSpace(text[nextPos]))
        {
            nextPos++;
        }

        if (nextPos >= text.Length)
            return true;

        char nextChar = text[nextPos];

        if (char.IsUpper(nextChar))
        {
            int wordStart = position - 1;
            while (wordStart >= 0 && char.IsLetter(text[wordStart]))
            {
                wordStart--;
        MainThread.BeginInvokeOnMainThread(() =>
        {
            PageLabel.Text = $"Page: {_currentPage}";
        });
                "mr", "mrs", "ms", "dr", "prof", "st", "jr", "sr",
                "inc", "ltd", "co", "corp", "eg", "ie", "etc", "vs"
            };

            if (abbreviations.Contains(word))
                return false;

            if (word.Length == 1)
                return false;

            return true;
        }

        return false;
    }

    private async Task UpdateHighlightDisplay(string text, int pageNum)
    {
        MainThread.BeginInvokeOnMainThread(() =>
        {
            HighlightLabel.Text = text;
            HighlightLabel.BackgroundColor = Colors.Yellow;

            var animation = new Animation(v => HighlightLabel.BackgroundColor =
                Color.FromRgba(255, 255, 0, v), 1, 0.3);
            animation.Commit(HighlightLabel, "HighlightFade", length: 2000);
        });
    }

    private async Task UpdatePdfViewerToPage(int pageNumber)
    {
        lock (_lockObject)
        {
            _currentPage = Math.Clamp(pageNumber, 1, _totalPages);
        }

        MainThread.BeginInvokeOnMainThread(() =>
        {
            PageLabel.Text = $"Page: {_currentPage}";
        });

        // Note: PDF viewer update using JavaScript scroll to page
        // This depends on your PDF viewer's capabilities
        try
        {
            await PdfWebView.EvaluateJavaScriptAsync($"scrollToPage({_currentPage})");
        }
        catch
        {
            // JavaScript not available or PDF viewer doesn't support scrolling
            // You may need to reload the PDF with a different approach
        }
    }

    private string CleanTextForReading(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        text = Regex.Replace(text, @"\s+", " ");
        text = Regex.Replace(text, @"\r\n|\n\r|\n|\r", " ");

        return text.Trim();
    }

    private async void OnNextPageClicked(object sender, EventArgs e)
    {
        lock (_lockObject)
        {
            if (_currentPage < _totalPages)
            {
                _currentPage++;

                if (_isReading)
                {
                    _isPaused = true;
                    _cancellationTokenSource?.Cancel();
                    _currentChunkIndex = 0;
                    _currentPageChunks.Clear();
                }
            }
        }

        await UpdatePdfViewerToPage(_currentPage);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            StatusLabel.Text = $"Moved to page {_currentPage}";
        });
    }

    private async void OnPreviousPageClicked(object sender, EventArgs e)
    {
        lock (_lockObject)
        {
            if (_currentPage > 1)
            {
                _currentPage--;

                if (_isReading)
                {
                    _isPaused = true;
                    _cancellationTokenSource?.Cancel();
                    _currentChunkIndex = 0;
                    _currentPageChunks.Clear();
                }
            }
        }

        await UpdatePdfViewerToPage(_currentPage);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            StatusLabel.Text = $"Moved to page {_currentPage}";
        });
    }

    private async Task SaveReadingPositionAsync()
    {
        try
        {
            lock (_lockObject)
            {
                Preferences.Set($"{_filePath}_Page", _currentPage);
                Preferences.Set($"{_filePath}_Chunk", _currentChunkIndex);
                Preferences.Set($"{_filePath}_Rate", _speechRate);
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error saving position: {ex.Message}");
        }

        await Task.CompletedTask;
    }

    private async Task LoadReadingPositionAsync()
    {
        try
        {
            int savedPage = Preferences.Get($"{_filePath}_Page", 1);
            int savedChunk = Preferences.Get($"{_filePath}_Chunk", 0);
            double savedRate = Preferences.Get($"{_filePath}_Rate", 0.4);

            if (savedPage > 1)
            {
                lock (_lockObject)
        base.OnAppearing();
        await LoadPdfAsync();
        await LoadReadingPositionAsync();
    }

    protected override async void OnDisappearing()
    {
        _isDisposing = true;

        try
        {
            await SaveReadingPositionAsync();

            // Cancel any ongoing speech
            _cancellationTokenSource?.Cancel();

            // Wait a bit for cancellation to complete
            await Task.Delay(100);

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            _isPaused = true;
            _isReading = false;

            // Clear dictionaries to free memory
            lock (_lockObject)
            {
                _pageFullText.Clear();
                _currentPageChunks.Clear();
            }

            _pdfDocument?.Dispose();
            _pdfDocument = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error loading position: {ex.Message}");
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadPdfAsync();
        await LoadReadingPositionAsync();
    }

    {
        base.OnAppearing();
        await LoadPdfAsync();
        await LoadReadingPositionAsync();
    }

    protected override async void OnDisappearing()
    {
        _isDisposing = true;

        try
        {
            await SaveReadingPositionAsync();

            // Cancel any ongoing speech
            _cancellationTokenSource?.Cancel();

            // Wait a bit for cancellation to complete
            await Task.Delay(100);

            _cancellationTokenSource?.Dispose();
            _cancellationTokenSource = null;

            _isPaused = true;
            _isReading = false;

            // Clear dictionaries to free memory
            lock (_lockObject)
            {
                _pageFullText.Clear();
                _currentPageChunks.Clear();
            }

            _pdfDocument?.Dispose();
            _pdfDocument = null;
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during cleanup: {ex.Message}");
        }
        finally
        {
            base.OnDisappearing();
        }
    }
}
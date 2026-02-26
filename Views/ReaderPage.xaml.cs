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
    private double _speechRate = 0.2;
    private const double MinRate = 0.25;
    private const double MaxRate = 1.0;
    private int _currentPage = 1;
    private int _currentChunkIndex = 0;
    private Dictionary<int, string> _pageFullText = new Dictionary<int, string>();
    private int _totalPages = 0;
    private PdfDocument? _pdfDocument;
    private CancellationTokenSource? _cancellationTokenSource;
    private List<string> _currentPageChunks = new List<string>(); // Store chunks for current page

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
            StatusLabel.Text = "Error loading PDF";
        }

        PageLabel.Text = $"Page: {_currentPage}";
        StatusLabel.Text = $"Loaded {_totalPages} pages";
    }

    private void OnSlowClicked(object sender, EventArgs e)
    {
        _speechRate -= 0.1;

        if (_speechRate < MinRate)
            _speechRate = MinRate;

        SpeedLabel.Text = $"Speed: {_speechRate:F1}x";
    }

    private void OnFastClicked(object sender, EventArgs e)
    {
        _speechRate += 0.1;

        if (_speechRate > MaxRate)
            _speechRate = MaxRate;

        SpeedLabel.Text = $"Speed: {_speechRate:F1}x";
    }

    private async Task LoadPdfAsync()
    {
        await Task.Delay(100); // let UI render first

        MainThread.BeginInvokeOnMainThread(() =>
        {
            PdfWebView.Source = new UrlWebViewSource
            {
                Url = $"file:///{_filePath.Replace("\\", "/")}"
            };
        });
    }

    [Obsolete]
    private async void OnReadResumeClicked(object sender, EventArgs e)
    {
        if (_isReading && !_isPaused)
        {
            return; // Already reading
        }

        if (!_isReading) // Starting fresh
        {
            string result = await DisplayPromptAsync(
                "Start Reading",
                $"Enter page number to start from (1-{_totalPages}):",
                initialValue: _currentPage.ToString(),
                keyboard: Keyboard.Numeric);

            if (!int.TryParse(result, out int startPage) || startPage < 1 || startPage > _totalPages)
            {
                await DisplayAlert("Invalid", $"Please enter a valid page number between 1 and {_totalPages}.", "OK");
                return;
            }

            _currentPage = startPage;
            _currentChunkIndex = 0; // Start from beginning
            _currentPageChunks.Clear(); // Clear previous chunks

            // Update the PDF viewer
            await UpdatePdfViewerToPage(_currentPage);
        }

        _isPaused = false;
        _isReading = true;
        StatusLabel.Text = $"Reading page {_currentPage}";

        // Start or continue reading
        await ReadFromCurrentPosition();
    }

    private void OnPauseClicked(object sender, EventArgs e)
    {
        if (_isReading)
        {
            _isPaused = true;
            StatusLabel.Text = $"Paused at page {_currentPage}";

            // Cancel any ongoing speech
            _cancellationTokenSource?.Cancel();
        }
    }

    [Obsolete]
    private async Task ReadFromCurrentPosition()
    {
        _cancellationTokenSource = new CancellationTokenSource();

        try
        {
            // Start from current page
            for (int pageNum = _currentPage; pageNum <= _totalPages; pageNum++)
            {
                if (_isPaused || _cancellationTokenSource.Token.IsCancellationRequested)
                    break;

                _currentPage = pageNum;

                // Update UI
                MainThread.BeginInvokeOnMainThread(() =>
                {
                    PageLabel.Text = $"Page: {_currentPage}";
                    ReadingProgress.Progress = (double)_currentPage / _totalPages;
                    StatusLabel.Text = $"Reading page {_currentPage}";
                });

                // Update PDF viewer
                await UpdatePdfViewerToPage(pageNum);

                if (!_pageFullText.ContainsKey(pageNum))
                {
                    if (_pdfDocument != null)
                    {
                        var page = _pdfDocument.GetPage(pageNum);
                        _pageFullText[pageNum] = page.Text;
                    }
                }

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
                {
                    _isReading = false;
                    _currentChunkIndex = 0;
                    _currentPageChunks.Clear();
                    StatusLabel.Text = "Reading complete";
                    ReadingProgress.Progress = 1.0;
                });

                await DisplayAlert("Complete", "Finished reading the document.", "OK");
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error reading: {ex.Message}");
            StatusLabel.Text = "Error during reading";
        }
    }

    private async Task ReadPageFromPosition(string pageText, int pageNum)
    {
        // Clean the text
        var cleanText = CleanTextForReading(pageText);

        // Split into chunks (only once per page)
        if (_currentPageChunks.Count == 0)
        {
            _currentPageChunks = SplitIntoSmartChunks(cleanText);
        }

        // Start from saved chunk index
        for (int i = _currentChunkIndex; i < _currentPageChunks.Count; i++)
        {
            if (_isPaused || _cancellationTokenSource.Token.IsCancellationRequested)
            {
                // Save position BEFORE returning
                _currentChunkIndex = i;
                return;
            }

            var chunk = _currentPageChunks[i];

            // Update highlight display
            await UpdateHighlightDisplay(chunk, pageNum);

            // Read the chunk
            await TextToSpeech.SpeakAsync(
                 chunk,
                 new SpeechOptions
                 {
                     Rate = (float)_speechRate
                 }
             );


            // Small delay between chunks
            await Task.Delay(30);
        }

        // If we finished all chunks on this page, reset for next page
        _currentChunkIndex = 0;
        _currentPageChunks.Clear();
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
        // Replace "Mr ." with "Mr." (with space before period)
        text = Regex.Replace(text, @"\b(Mr|Mrs|Ms|Dr|Prof|St|Jr|Sr)\s*\.", "$1.", RegexOptions.IgnoreCase);

        // Fix other common abbreviations
        text = Regex.Replace(text, @"\b(Inc|Ltd|Co|Corp|eg|ie|etc|vs)\s*\.", "$1.", RegexOptions.IgnoreCase);

        // Fix initials: "J . K ." -> "J.K."
        text = Regex.Replace(text, @"\b([A-Z])\s*\.\s*([A-Z])\s*\.", "$1.$2.");

        // Fix single initials: "J ." -> "J."
        text = Regex.Replace(text, @"\b([A-Z])\s*\.", "$1.");

        return text;
    }

    private List<string> SplitParagraphIntoSentences(string paragraph)
    {
        var sentences = new List<string>();
        if (string.IsNullOrWhiteSpace(paragraph))
            return sentences;

        // Simple state machine to handle sentences with abbreviations
        int start = 0;
        int position = 0;

        while (position < paragraph.Length)
        {
            char c = paragraph[position];

            // Check for potential sentence endings
            if (c == '.' || c == '!' || c == '?')
            {
                // Check if it's really a sentence end (not an abbreviation)
                bool isSentenceEnd = IsTrueSentenceEnd(paragraph, position);

                if (isSentenceEnd)
                {
                    // Extract the sentence
                    string sentence = paragraph.Substring(start, position - start + 1).Trim();
                    if (!string.IsNullOrWhiteSpace(sentence))
                    {
                        sentences.Add(sentence);
                    }

                    // Move to next potential sentence
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

        // Add any remaining text
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
        // Check if the period at 'position' is a true sentence end

        // If at end of text, it's a sentence end
        if (position >= text.Length - 1)
            return true;

        // Look at next non-whitespace character
        int nextPos = position + 1;
        while (nextPos < text.Length && char.IsWhiteSpace(text[nextPos]))
        {
            nextPos++;
        }

        if (nextPos >= text.Length)
            return true;

        char nextChar = text[nextPos];

        // If next character is uppercase, it's likely a new sentence
        // But we need to check if this was an abbreviation
        if (char.IsUpper(nextChar))
        {
            // Check if the word before the period is an abbreviation
            int wordStart = position - 1;
            while (wordStart >= 0 && char.IsLetter(text[wordStart]))
            {
                wordStart--;
            }

            wordStart++; // Move to first letter

            string word = text.Substring(wordStart, position - wordStart).ToLower();

            // Common abbreviations that DON'T end sentences
            var abbreviations = new HashSet<string>
            {
                "mr", "mrs", "ms", "dr", "prof", "st", "jr", "sr",
                "inc", "ltd", "co", "corp", "eg", "ie", "etc", "vs"
            };

            // If it's an abbreviation, it's NOT a sentence end
            if (abbreviations.Contains(word))
                return false;

            // Single letters are usually initials, not sentence ends
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
            // Show the text being read
            HighlightLabel.Text = text;
            HighlightLabel.BackgroundColor = Colors.Yellow;

            // Animate the highlight
            var animation = new Animation(v => HighlightLabel.BackgroundColor =
                Color.FromRgba(255, 255, 0, v), 1, 0.3);
            animation.Commit(HighlightLabel, "HighlightFade", length: 2000);
        });
    }

    private async Task UpdatePdfViewerToPage(int pageNumber)
    {
        _currentPage = Math.Clamp(pageNumber, 1, _totalPages);

        MainThread.BeginInvokeOnMainThread(() =>
        {
            PageLabel.Text = $"Page: {_currentPage}";
        });

        // Do NOT reload WebView anymore
    }

    private string CleanTextForReading(string text)
    {
        if (string.IsNullOrWhiteSpace(text))
            return text;

        // Remove multiple spaces
        text = Regex.Replace(text, @"\s+", " ");

        // Fix line breaks
        text = Regex.Replace(text, @"\r\n|\n\r|\n|\r", " ");

        return text.Trim();
    }

    private async void OnNextPageClicked(object sender, EventArgs e)
    {
        if (_currentPage < _totalPages)
        {
            _currentPage++;
            await UpdatePdfViewerToPage(_currentPage);

            if (_isReading)
            {
                _isPaused = true;
                StatusLabel.Text = $"Moved to page {_currentPage}";
                _currentChunkIndex = 0;
                _currentPageChunks.Clear();
                await Task.Delay(100);
            }
        }
    }

    private async void OnPreviousPageClicked(object sender, EventArgs e)
    {
        if (_currentPage > 1)
        {
            _currentPage--;
            await UpdatePdfViewerToPage(_currentPage);

            if (_isReading)
            {
                _isPaused = true;
                StatusLabel.Text = $"Moved to page {_currentPage}";
                _currentChunkIndex = 0;
                _currentPageChunks.Clear();
                await Task.Delay(100);
            }
        }
    }

    protected override async void OnAppearing()
    {
        base.OnAppearing();
        await LoadPdfAsync();
    }
    protected override void OnDisappearing()
    {
        base.OnDisappearing();
        //await SaveReadingPositionAsync();
        _cancellationTokenSource?.Cancel();
        _isPaused = true;
        _isReading = false;

        _pdfDocument?.Dispose();
    }
}
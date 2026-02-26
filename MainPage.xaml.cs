using pdfreader.Models;
using pdfreader.Views;
using System.Collections.ObjectModel;
using System.Runtime.Versioning;

namespace pdfreader
{
    public partial class MainPage : ContentPage
    {
         public ObservableCollection<BookItem> Books { get; set; } = new();

        public MainPage()
        {
            InitializeComponent();
            LoadBooks();
            BooksCollection.ItemsSource = Books; 
        }
        async void LoadBooks()
        {
            var booksFromDb = await App.Database.GetBooksAsync();
            Books.Clear();
            foreach (var book in booksFromDb)
            {
                Books.Add(book);
            }
        }

        [SupportedOSPlatform("windows10.0.17763")]
        private async void OnAddBookClicked(object? sender, EventArgs e)
        {
            var result = await FilePicker.PickAsync();
            if (result != null)
            {
                var Book = new BookItem
                {
                    Title = Path.GetFileNameWithoutExtension(result.FileName),
                    FilePath = result.FullPath,
                    FileType = Path.GetExtension(result.FileName)
                };
                await App.Database.SaveBookAsync(Book);
                Books.Add(Book);
            }
        }
        private async void OnBookSelected(object? sender, SelectionChangedEventArgs? e)
        {
            if (e?.CurrentSelection.FirstOrDefault() is BookItem selectedBook)
            {
                    await Navigation.PushAsync(new ReaderPage(selectedBook.FilePath));
            }

            if (sender is CollectionView cv)
                cv.SelectedItem = null;
        }

        private async void OnDeleteBookClicked(object sender, EventArgs e)
        {
            if (sender is Button button && button.CommandParameter is BookItem book)
            {
                bool confirm = await DisplayAlertAsync(
                    "Delete Book",
                    $"Are you sure you want to delete '{book.Title}'?",
                    "Yes",
                    "No");

                if (!confirm)
                    return;

                // 1️⃣ Delete from database
                await App.Database.DeleteBookAsync(book);

                // 2️⃣ Remove from ObservableCollection (updates UI automatically)
                Books.Remove(book);

                // 3️⃣ OPTIONAL: Delete physical file
                if (File.Exists(book.FilePath))
                {
                    File.Delete(book.FilePath);
                }
            }
        }

    }
}

using SQLite;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Text;

namespace pdfreader.Models
{
    public class BookItem
    {
        [PrimaryKey, AutoIncrement]
        public int Id { get; set; }
        public string? Title { get; set; }
        public string? FilePath { get; set; }

        public int LastPosition { get; set; }// (later) get the last position in the book, so that the user can continue reading from where they left off
        public string? FileType { get; set; }
    }

    public class BookCollection
    {
        public ObservableCollection<BookItem> Books { get; set; } = new();
    }

}

using pdfreader.Models;
using SQLite;
using System;
using System.Collections.Generic;
using System.Text;

namespace pdfreader.Data
{
    internal class LibraryDatabase
    {
        readonly SQLiteAsyncConnection _database;
        public LibraryDatabase(string dbPath)
        {
            _database = new SQLiteAsyncConnection(dbPath);
            _database.CreateTableAsync<BookItem>().Wait();
        }
        public Task<List<BookItem>> GetBooksAsync()
        {
            return _database.Table<BookItem>().ToListAsync();
        }
        public Task<int> SaveBookAsync(BookItem book)
        {
            if (book.Id != 0)
            {
                return _database.UpdateAsync(book);
            }
            else
            {
                return _database.InsertAsync(book);
            }
        }
        public Task<int> DeleteBookAsync(BookItem book)
        {
            return _database.DeleteAsync(book);
        }

        public Task<BookItem> UpdateBookAsync(BookItem book)
        {
            return _database.UpdateAsync(book).ContinueWith(t => book);
        }


    }

}

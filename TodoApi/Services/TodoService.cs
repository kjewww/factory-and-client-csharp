using Microsoft.EntityFrameworkCore;
using TodoApi.Data;
using TodoApi.Models;

namespace TodoApi.Services;

public class TodoService : ITodoService
{
    private readonly TodoDbContext _db;

    public TodoService(TodoDbContext db)
    {
        _db = db;
    }

    // GET ALL
    public async Task<List<Todo>> GetAllAsync()
    {
        return await _db.Todos.ToListAsync();
    }

    // GET BY ID
    public async Task<Todo?> GetByIdAsync(int id)
    {
        return await _db.Todos.FindAsync(id);
    }

    // CREATE
    public async Task<Todo> CreateAsync(Todo todo)
    {
        _db.Todos.Add(todo);

        await _db.SaveChangesAsync();

        return todo;
    }

    // UPDATE
    public async Task<bool> UpdateAsync(int id, Todo todo)
    {
        var existingTodo = await _db.Todos.FindAsync(id);

        if (existingTodo == null)
        {
            return false;
        }

        existingTodo.Title = todo.Title;
        existingTodo.IsCompleted = todo.IsCompleted;

        await _db.SaveChangesAsync();

        return true;
    }

    // DELETE
    public async Task<bool> DeleteAsync(int id)
    {
        var todo = await _db.Todos.FindAsync(id);

        if (todo == null)
        {
            return false;
        }

        _db.Todos.Remove(todo);

        await _db.SaveChangesAsync();

        return true;
    }
}
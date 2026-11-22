using Microsoft.AspNetCore.Mvc;
using TodoApi.Models;
using TodoApi.Services;

namespace TodoApi.Controllers;

[ApiController]
[Route("api/todos")]
public class TodosController : ControllerBase
{
    private readonly ITodoService _service;

    public TodosController(ITodoService service)
    {
        _service = service;
    }

    // GET: /api/todos
    [HttpGet]
    public async Task<IActionResult> GetAll()
    {
        var todos = await _service.GetAllAsync();

        return Ok(todos);
    }

    // GET: /api/todos/{id}
    [HttpGet("{id}")]
    public async Task<IActionResult> GetById(int id)
    {
        var todo = await _service.GetByIdAsync(id);

        if (todo == null)
        {
            return NotFound();
        }

        return Ok(todo);
    }

    // POST: /api/todos
    [HttpPost]
    public async Task<IActionResult> Create(Todo todo)
    {
        var createdTodo = await _service.CreateAsync(todo);

        return CreatedAtAction(
            nameof(GetById),
            new { id = createdTodo.Id },
            createdTodo
        );
    }

    // PUT: /api/todos/{id}
    [HttpPut("{id}")]
    public async Task<IActionResult> Update(int id, Todo todo)
    {
        var success = await _service.UpdateAsync(id, todo);

        if (!success)
        {
            return NotFound();
        }

        return NoContent();
    }

    // DELETE: /api/todos/{id}
    [HttpDelete("{id}")]
    public async Task<IActionResult> Delete(int id)
    {
        var success = await _service.DeleteAsync(id);

        if (!success)
        {
            return NotFound();
        }

        return NoContent();
    }
}
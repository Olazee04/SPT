using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using SPT.Data;
using SPT.Models;

namespace SPT.Controllers
{
    [Authorize(Roles = "Admin,Mentor")]
    public class QuizController : Controller
    {
        private readonly ApplicationDbContext _context;

        public QuizController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================
        // 1. SELECT MODULE (Step 1)
        // =========================
        [HttpGet]
        public async Task<IActionResult> Index()
        {
            // List all modules so Admin can pick one to manage
            var modules = await _context.SyllabusModules
                .Include(m => m.Track)
                .Include(m => m.Questions)
                .OrderBy(m => m.Track.Name)
                .ThenBy(m => m.DisplayOrder)
                .ToListAsync();

            return View(modules);
        }

        // =========================
        // 2. MANAGE QUESTIONS (Step 2)
        // =========================
        [HttpGet]
        public async Task<IActionResult> Manage(int moduleId)
        {
            var module = await _context.SyllabusModules
                .Include(m => m.Track)
                .FirstOrDefaultAsync(m => m.Id == moduleId);

            if (module == null) return NotFound();

            var questions = await _context.QuizQuestions
                .Include(q => q.Options)
                .Where(q => q.ModuleId == moduleId)
                .ToListAsync();

            ViewBag.Module = module;
            return View(questions);
        }

        // =========================
        // 3. CREATE QUESTION (GET)
        // =========================
        [HttpGet]
        public IActionResult CreateQuestion(int moduleId)
        {
            ViewBag.ModuleId = moduleId;
            return View();
        }

        // =========================
        // 4. CREATE QUESTION (POST)
        // =========================
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateQuestion(int moduleId, string questionText, List<string> options, int correctIndex)
        {
            if (string.IsNullOrEmpty(questionText) || options == null || options.Count < 2)
            {
                TempData["Error"] = "Question and at least 2 options are required.";
                return RedirectToAction(nameof(Manage), new { moduleId });
            }

            // Save Question
            var question = new QuizQuestion
            {
                ModuleId = moduleId,
                QuestionText = questionText
            };
            _context.QuizQuestions.Add(question);
            await _context.SaveChangesAsync(); // Save to get ID

            // Save Options
            for (int i = 0; i < options.Count; i++)
            {
                if (!string.IsNullOrWhiteSpace(options[i]))
                {
                    _context.QuizOptions.Add(new QuizOption
                    {
                        QuestionId = question.Id,
                        OptionText = options[i],
                        IsCorrect = (i == correctIndex) // The radio button index determines truth
                    });
                }
            }
            await _context.SaveChangesAsync();

            TempData["Success"] = "Question added successfully!";
            return RedirectToAction(nameof(Manage), new { moduleId });
        }

        // =========================
        // 5. DELETE QUESTION
        // =========================
        [HttpPost]
        public async Task<IActionResult> DeleteQuestion(int id)
        {
            var q = await _context.QuizQuestions.FindAsync(id);
            if (q != null)
            {
                int modId = q.ModuleId;
                _context.QuizQuestions.Remove(q);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Manage), new { moduleId = modId });
            }
            return RedirectToAction(nameof(Index));
        }
    }
}
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
        private readonly UserManager<ApplicationUser> _userManager;

        public QuizController(ApplicationDbContext context, UserManager<ApplicationUser> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        // =========================
        // 1. SELECT MODULE (Step 1)
        // =========================
        [HttpGet]
        public async Task<IActionResult> Index(int? trackId)
        {
            // 1. Get Tracks for Dropdown
            ViewBag.Tracks = await _context.Tracks.ToListAsync();

            // 2. Start Query for Modules
            var query = _context.SyllabusModules
                .Include(m => m.Track)
                .Include(m => m.Questions) // Include questions so we can count them
                .AsQueryable();

            // 3. Filter if needed
            if (trackId.HasValue)
            {
                query = query.Where(m => m.TrackId == trackId.Value);
            }

            // 4. Get List
            var modules = await query
                .OrderBy(m => m.Track.Name)
                .ThenBy(m => m.DisplayOrder)
                .ToListAsync();

            // 5. Send MODULES to the View
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

        // =========================
        // STUDENT: TAKE QUIZ
        // =========================
        [HttpGet]
        [Authorize(Roles = "Student")]
        public async Task<IActionResult> Take(int moduleId)
        {
            var module = await _context.SyllabusModules
                .Include(m => m.Track)
                .FirstOrDefaultAsync(m => m.Id == moduleId);

            if (module == null) return NotFound();

            var questions = await _context.QuizQuestions
                .Include(q => q.Options)
                .Where(q => q.ModuleId == moduleId)
                .ToListAsync();

            if (!questions.Any())
            {
                TempData["Error"] = "This quiz is not ready yet.";
                return RedirectToAction("Dashboard", "Student");
            }

            ViewBag.Module = module;
            return View(questions);
        }

        // =========================
        // STUDENT: SUBMIT QUIZ
        // =========================
        [HttpPost]
        [Authorize(Roles = "Student")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Submit(int moduleId, Dictionary<int, int> answers)
        {
            // 1. Fetch Questions to check answers
            var questions = await _context.QuizQuestions
                .Include(q => q.Options)
                .Where(q => q.ModuleId == moduleId)
                .ToListAsync();

            int correctCount = 0;
            int totalQuestions = questions.Count;

            foreach (var q in questions)
            {
                // Check if user answered this question
                if (answers.ContainsKey(q.Id))
                {
                    var selectedOptionId = answers[q.Id];
                    var correctOption = q.Options.FirstOrDefault(o => o.IsCorrect);

                    if (correctOption != null && correctOption.Id == selectedOptionId)
                    {
                        correctCount++;
                    }
                }
            }

            // 2. Calculate Score
            double score = totalQuestions > 0 ? ((double)correctCount / totalQuestions) * 100 : 0;
            int finalScore = (int)Math.Round(score);

            // 3. Save Score (Update Module Completion)
            var user = await _userManager.GetUserAsync(User);
            var student = await _context.Students.FirstOrDefaultAsync(s => s.UserId == user.Id);

            var completion = await _context.ModuleCompletions
                .FirstOrDefaultAsync(mc => mc.StudentId == student.Id && mc.ModuleId == moduleId);

            if (completion == null)
            {
                completion = new ModuleCompletion
                {
                    StudentId = student.Id,
                    ModuleId = moduleId,
                    IsCompleted = finalScore >= 70 // Pass mark example
                };
                _context.ModuleCompletions.Add(completion);
            }

            
            await _context.SaveChangesAsync();

            // 4. Show Result
            TempData["QuizResult_Score"] = finalScore;
            TempData["QuizResult_Total"] = totalQuestions;
            TempData["QuizResult_Correct"] = correctCount;

            return RedirectToAction("QuizResult", new { moduleId });
        }

        [HttpGet]
        public IActionResult QuizResult(int moduleId)
        {
            return View();
        }
    }
}
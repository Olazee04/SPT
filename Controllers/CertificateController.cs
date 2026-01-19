using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using SPT.Data;
using SPT.Models;

namespace SPT.Controllers
{
    [Authorize(Roles = "Admin")]
    public class CertificateController : Controller
    {
        private readonly ApplicationDbContext _context;

        public CertificateController(ApplicationDbContext context)
        {
            _context = context;
        }

        // =========================
        // 1. LIST ISSUED CERTIFICATES
        // =========================
        public async Task<IActionResult> Index()
        {
            var certs = await _context.Certificates
                .Include(c => c.Student)
                .OrderByDescending(c => c.DateIssued)
                .ToListAsync();
            return View(certs);
        }

        // =========================
        // 2. CHECK ELIGIBILITY (Who can graduate?)
        // =========================
        public async Task<IActionResult> Eligibility()
        {
            var students = await _context.Students
                .Include(s => s.Track)
                .Include(s => s.ModuleCompletions)
                .Include(s => s.ProgressLogs)
                .Where(s => s.EnrollmentStatus == "Active")
                .ToListAsync();

            var eligibleList = new List<dynamic>();

            foreach (var s in students)
            {
                // Logic: Must verify 90% of total track hours or modules
                // For simplicity here: Check if verified hours > 100 (example threshold)
                // You can make this stricter later.
                decimal totalHours = s.ProgressLogs.Where(l => l.IsApproved).Sum(l => l.Hours);

                // Check if already has cert
                bool hasCert = await _context.Certificates.AnyAsync(c => c.StudentId == s.Id);

                if (!hasCert)
                {
                    eligibleList.Add(new
                    {
                        Student = s,
                        TotalHours = totalHours,
                        IsReady = totalHours >= 50 // Example threshold: 50 hours to graduate
                    });
                }
            }

            ViewBag.EligibleList = eligibleList;
            return View();
        }

        // =========================
        // 3. ISSUE CERTIFICATE
        // =========================
        [HttpPost]
        public async Task<IActionResult> Issue(int studentId)
        {
            var student = await _context.Students.Include(s => s.Track).FirstOrDefaultAsync(s => s.Id == studentId);
            if (student == null) return NotFound();

            var cert = new Certificate
            {
                StudentId = student.Id,
                TrackName = student.Track?.Name ?? "Software Engineering",
                DateIssued = DateTime.UtcNow,
                IssuedBy = User.Identity.Name ?? "Admin"
            };

            _context.Certificates.Add(cert);
            await _context.SaveChangesAsync();

            TempData["Success"] = $"Certificate issued to {student.FullName}!";
            return RedirectToAction(nameof(Index));
        }

        // =========================
        // 4. VIEW / PRINT CERTIFICATE
        // =========================
        [AllowAnonymous] // Allow public verification if needed
        public async Task<IActionResult> ViewCertificate(string id)
        {
            var cert = await _context.Certificates
                .Include(c => c.Student)
                .FirstOrDefaultAsync(c => c.CertificateId == id || c.Id.ToString() == id);

            if (cert == null) return NotFound();

            return View(cert);
        }
    }
}
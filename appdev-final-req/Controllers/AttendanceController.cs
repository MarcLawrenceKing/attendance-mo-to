using appdev_final_req.Data;
using appdev_final_req.Models;
using appdev_final_req.Models.Entitiess;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace appdev_final_req.Controllers
{
    [Authorize]
    public class AttendanceController : Controller
    {
        private readonly ApplicationDbContext dbContext;

        public AttendanceController(ApplicationDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        // Paginated list of events
        [HttpGet]
        public IActionResult List(string search, int page = 1, int pageSize = 5)
        {
            var query = dbContext.Events.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(e =>
                    e.Title.ToLower().Contains(search.ToLower()) ||
                    e.EventDate.ToString().Contains(search)
                );
            }

            int totalEvents = query.Count();
            int totalPages = (int)Math.Ceiling(totalEvents / (double)pageSize);

            var events = query
                .OrderBy(e => e.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.SearchQuery = search;

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("List", events);
            }

            return View(events);
        }

        [HttpGet]
        public async Task<IActionResult> MarkAttendance(int id, string? search, int page = 1, int pageSize = 10)
        {
            var eventInfo = await dbContext.Events.FindAsync(id);
            if (eventInfo == null) return NotFound();

            var membersQuery = dbContext.Members.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                membersQuery = membersQuery.Where(m =>
                    m.FullName.ToLower().Contains(search) ||
                    m.Id.ToString().Contains(search)
                );
            }

            int totalMembers = await membersQuery.CountAsync();
            int totalPages = (int)Math.Ceiling(totalMembers / (double)pageSize);

            var members = await membersQuery
                .OrderBy(m => m.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var existing = await dbContext.Attendance
                .Where(a => a.EventId == id)
                .ToListAsync();

            var viewModel = members.Select(m => new AttendanceViewModel
            {
                MemberId = m.Id,
                FullName = m.FullName,
                IsPresent = existing.FirstOrDefault(a => a.MemberId == m.Id)?.IsPresent ?? false
            }).ToList();

            ViewBag.EventId = id;
            ViewBag.EventTitle = eventInfo.Title;
            ViewBag.Saved = false;
            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.SearchQuery = search;

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("MarkAttendance", viewModel);
            }

            return View(viewModel);
        }


        [HttpPost]
        public async Task<IActionResult> MarkAttendance(int eventId, List<AttendanceViewModel> attendanceList)
        {
            var existing = await dbContext.Attendance
                .Where(a => a.EventId == eventId)
                .ToListAsync();

            foreach (var item in attendanceList)
            {
                var record = existing.FirstOrDefault(a => a.MemberId == item.MemberId);
                
                if (record != null)
                {
                    record.IsPresent = item.IsPresent;
                }
                else
                {
                    dbContext.Attendance.Add(new Attendance
                    {
                        MemberId = item.MemberId,
                        EventId = eventId,
                        IsPresent = item.IsPresent
                    });
                }
            }

            await dbContext.SaveChangesAsync();
            await UpdateMemberActivityStatusAsync();

            return RedirectToAction("List");
        }

        [HttpPost]
        private async Task UpdateMemberActivityStatusAsync()
        {
            var sql = @"
            DECLARE @TotalEvents INT;
            SELECT @TotalEvents = COUNT(*) FROM Events;

            UPDATE Members
            SET IsActive = 1
            WHERE Id IN (
                SELECT MemberId
                FROM Attendance
                WHERE IsPresent = 1
                GROUP BY MemberId
                HAVING COUNT(*) * 1.0 / NULLIF(@TotalEvents, 0) >= 0.5
            );

            UPDATE Members
            SET IsActive = 0
            WHERE Id NOT IN (
                SELECT MemberId
                FROM Attendance
                WHERE IsPresent = 1
                GROUP BY MemberId
                HAVING COUNT(*) * 1.0 / NULLIF(@TotalEvents, 0) >= 0.5
            );
        ";

            await dbContext.Database.ExecuteSqlRawAsync(sql);
        }
    }
}

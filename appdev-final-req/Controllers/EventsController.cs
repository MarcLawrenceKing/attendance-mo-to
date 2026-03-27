using appdev_final_req.Data;
using appdev_final_req.Models.Entitiess;
using appdev_final_req.Models;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using CsvHelper;
using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using CsvHelper.Configuration;

namespace appdev_final_req.Controllers
{
    [Authorize]
    public class EventsController : Controller
    {
        private readonly ApplicationDbContext dbContext;

        public EventsController(ApplicationDbContext dbContext)
        {
            this.dbContext = dbContext;
        }

        [HttpGet]
        public async Task<IActionResult> List(string search, int page = 1, int pageSize = 5)
        {
            var query = dbContext.Events.AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(e =>
                    e.Title.ToLower().Contains(search.ToLower()) ||
                    e.Description.ToLower().Contains(search.ToLower()) ||
                    e.EventDate.ToString().Contains(search)
                );
            }

            int totalEvents = await query.CountAsync();
            int totalPages = (int)Math.Ceiling(totalEvents / (double)pageSize);

            var events = await query
                .OrderBy(e => e.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            ViewBag.CurrentPage = page;
            ViewBag.TotalPages = totalPages;
            ViewBag.SearchQuery = search;

            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return PartialView("List", events);  // You need a partial List.cshtml view for AJAX
            }

            return View(events);
        }


        [HttpGet]
        public IActionResult Add()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Add(AddEventViewModel viewModel)
        {
            var eventz = new Event
            {
                Title = viewModel.Title,
                Description = viewModel.Description,
                EventDate = viewModel.EventDate,
            };

            await dbContext.Events.AddAsync(eventz);
            await dbContext.SaveChangesAsync();
            TempData["Message"] = "Event added successfully!";
            return RedirectToAction("List");
        }

        [HttpGet]
        public async Task<IActionResult> Edit(int id)
        {
            var eventz = await dbContext.Events.FindAsync(id);
            if (eventz == null) return NotFound();
            return View(eventz);
        }

        [HttpPost]
        public async Task<IActionResult> Edit(Event viewModel)
        {
            var eventz = await dbContext.Events.FindAsync(viewModel.Id);

            if (eventz is not null)
            {
                eventz.Title = viewModel.Title;
                eventz.Description = viewModel.Description;
                eventz.EventDate = viewModel.EventDate;

                await dbContext.SaveChangesAsync();
                TempData["Message"] = "Event updated successfully!";
            }

            return RedirectToAction("List");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var eventz = await dbContext.Events.FindAsync(id);

            if (eventz != null)
            {
                dbContext.Events.Remove(eventz);
                await dbContext.SaveChangesAsync();
                TempData["Message"] = "Event deleted successfully.";
            }

            return RedirectToAction("List");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteSelected(string ids)
        {
            if (!string.IsNullOrEmpty(ids))
            {
                var idList = ids.Split(',').Select(id => int.Parse(id)).ToList();

                var eventsToDelete = await dbContext.Events
                    .Where(e => idList.Contains(e.Id))
                    .ToListAsync();

                dbContext.Events.RemoveRange(eventsToDelete);
                await dbContext.SaveChangesAsync();

                TempData["Message"] = $"{eventsToDelete.Count} events deleted successfully.";
            }

            return RedirectToAction("List");
        }

        [HttpPost]
        public async Task<IActionResult> BatchUpload(IFormFile file)
        {
            try
            {
                if (file != null && file.Length > 0)
                {
                    using var reader = new StreamReader(file.OpenReadStream());

                    var config = new CsvConfiguration(CultureInfo.InvariantCulture)
                    {
                        PrepareHeaderForMatch = args => args.Header.ToLower(),
                        HeaderValidated = null,
                        MissingFieldFound = null,
                        BadDataFound = null
                    };

                    using var csv = new CsvReader(reader, config);

                    csv.Context.TypeConverterCache.AddConverter<DateOnly>(new DateOnlyConverter());

                    var events = csv.GetRecords<Event>().ToList();

                    dbContext.Events.AddRange(events);
                    await dbContext.SaveChangesAsync();

                    TempData["UploadMessage"] = $"{events.Count} events uploaded successfully!";
                    return RedirectToAction("List");
                }

                TempData["UploadMessage"] = "Please upload a valid CSV file.";
                return RedirectToAction("List");
            }
            catch (Exception ex)
            {
                TempData["UploadMessage"] = "Error uploading file: " + ex.Message;
                return RedirectToAction("List");
            }
        }
    }
}

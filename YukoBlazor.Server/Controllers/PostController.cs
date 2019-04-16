﻿using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Text;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using YukoBlazor.Server.Models;
using YukoBlazor.Shared;

namespace YukoBlazor.Server.Controllers
{
    [Route("api/[controller]")]
    public class PostController : Controller
    {
        private const int PageSize = 10;

        [HttpGet]
        public async Task<IActionResult> Get(
            [FromServices] BlogContext db,
            string title, DateTime? from, DateTime? to, 
            string catalog, string tag, bool isPage = false,
            int page = 0, CancellationToken token = default)
        {
            IQueryable<Post> query = db.Posts
                .Include(x => x.Tags)
                .Where(x => x.IsPage == isPage);

            if (!string.IsNullOrEmpty(title))
            {
                query = query.Where(x => x.Title.Contains(title));
            }

            if (!string.IsNullOrEmpty(catalog))
            {
                query = query.Where(x => x.Catalog.Id == catalog);
            }

            if (!string.IsNullOrEmpty(tag))
            {
                query = query.Where(x => x.Tags.Any(y => y.Tag == tag));
            }

            if (from.HasValue)
            {
                query = query.Where(x => x.Time >= from.Value);
            }

            if (to.HasValue)
            {
                query = query.Where(x => x.Time <= to.Value);
            }

            var dataCount = await query.CountAsync();
            var pageCount = (dataCount + PageSize - 1) / PageSize;
            var data = await query
                    .OrderByDescending(x => x.Time)
                    .Skip(page * PageSize)
                    .Take(PageSize)
                    .ToListAsync(token);

            return Json(new PagedViewModel<PostViewModel>
            {
                CurrentPage = page,
                TotalCount = dataCount,
                TotalPage = pageCount,
                Data = data.Select(x => new PostViewModel
                {
                    Catalog = x.Catalog,
                    Id = x.Id,
                    Content = x.Summary,
                    Tags = x.Tags.Select(y => y.Tag),
                    Time = x.Time,
                    Title = x.Title,
                    Url = x.Url
                })
            });
        }
        
        [HttpGet("{id}")]
        public async Task<IActionResult> Get(
            [FromServices] BlogContext db, string id,
            CancellationToken token = default)
        {
            var post = await db.Posts
                .Include(x => x.Tags)
                .SingleOrDefaultAsync(x => x.Url == id, token);

            if (post == null)
            {
                return Json(null);
            }
            else
            {
                return Json(new PostViewModel
                {
                    Id = post.Id,
                    Catalog = post.Catalog,
                    Content = post.Content,
                    Tags = post.Tags.Select(y => y.Tag),
                    Time = post.Time,
                    Title = post.Title,
                    Url = post.Url
                });
            }
        }

        [HttpPut("{url}")]
        [HttpPost("{url}")]
        public async Task<IActionResult> Put(
            [FromServices] BlogContext db, string url, 
            string catalog, string title, string tags, 
            string content, bool isPage = false, 
            CancellationToken token = default)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Json("Not Authorized");
            }

            if (await db.Posts.AnyAsync(x => x.Url == url, token))
            {
                return Json("URL is already exist");
            }

            if (!await db.Catalogs.AnyAsync(x => x.Id == catalog, token))
            {
                return Json("Catalog is not exist");
            }

            tags = tags ?? ""; // Defense null value of tags
            var tagsList = tags.Split(',')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrEmpty(x))
                .Select(x => new PostTag
                {
                    Tag = x
                })
                .ToList();

            var post = new Post
            {
                Url = url,
                CatalogId = catalog,
                IsPage = isPage,
                Tags = tagsList,
                Content = content,
                Title = title,
                Summary = TruncateContent(content)
            };

            db.Posts.Add(post);
            await db.SaveChangesAsync(token);
            return Json(post.Id);
        }

        [HttpPatch("{url}")]
        public async Task<IActionResult> Patch(
            [FromServices] BlogContext db, string content,
            string catalog, string title, string tags,
            string url, string newUrl, bool isPage = false,
            CancellationToken token = default)
        {
            if (!User.Identity.IsAuthenticated)
            {
                return Json("Not Authorized");
            }

            if (!string.IsNullOrWhiteSpace(url) 
                && await db.Posts.AnyAsync(x => x.Url == newUrl, token))
            {
                return Json("New URL is already exist");
            }

            if (!await db.Catalogs.AnyAsync(x => x.Id == catalog, token))
            {
                return Json("Catalog is not exist");
            }

            var post = await db.Posts
                .SingleOrDefaultAsync(x => x.Url == url, token);

            if (post == null)
            {
                return Json("Post is not exist");
            }

            tags = tags ?? ""; // Defense null value of tags
            var tagsList = tags.Split(',')
                .Select(x => x.Trim())
                .Where(x => !string.IsNullOrEmpty(x))
                .Select(x => new PostTag
                {
                    Tag = x
                })
                .ToList();

            post.Url = newUrl ?? url;
            post.CatalogId = catalog;
            post.Content = content;
            post.IsPage = isPage;
            post.Tags = tagsList;
            post.Title = title;
            post.Summary = TruncateContent(content);

            await db.SaveChangesAsync(token);
            return Json(post.Id);
        }

        private string TruncateContent(string content)
        {
            var summary = new StringBuilder();

            if (content != null)
            {
                var flag = false;
                var tmp = content
                    .Replace("\r", "")
                    .Split('\n');

                if (tmp.Count() > 10)
                {
                    for (var i = 0; i < 10; i++)
                    {
                        if (tmp[i].StartsWith("```"))
                        {
                            flag = !flag;
                        }

                        summary.AppendLine(tmp[i]);
                    }
                    if (flag)
                    {
                        summary.AppendLine("```");
                    }
                }
                else
                {
                    return content;
                }
            }

            return summary.ToString();
        }
    }
}

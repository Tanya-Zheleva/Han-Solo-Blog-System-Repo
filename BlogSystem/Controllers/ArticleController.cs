﻿using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.Data.Entity;
using System.Linq;
using System.Net;
using System.Web;
using System.Web.Mvc;
using BlogSystem.Models;
using BlogSystem.Extensions;

namespace BlogSystem.Controllers
{
    public class ArticleController : Controller
    {
        //
        // GET: Article
        public ActionResult Index()
        {
            return RedirectToAction("List");
        }

        //
        //GET: Article/List
        public ActionResult List()
        {
            using (var database = new BlogDbContext())
            {
                //Get articles from database
                var articles = database.Articles
                    .Include(a => a.Author)
                    .Include(a => a.Tags)
                    .ToList();

                return View(articles);
            }
        }

        //
        //GET: Article/Details
        public ActionResult Details(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadRequest);
            }

            using (var database = new BlogDbContext())
            {
                //Get the articles from database
                var article = database.Articles
                    .Where(a => a.Id == id)
                    .Include(a => a.Author)
                    .Include(a => a.Tags)
                    .First();

                if (article == null)
                {
                    return HttpNotFound();
                }

                return View(article);
            }
        }

        //
        //GET: Article/Create
        [Authorize]
        [Display(Name = "Category Id")]
        public ActionResult Create()
        {
            using (var database = new BlogDbContext())
            {
                var model = new ArticleViewModel();
                model.Categories = database.Categories
                    .OrderBy(c => c.Name)
                    .ToList();

                return View(model);
            }
        }

        //
        //POST: Article/Create
        [HttpPost]
        [Authorize]
        public ActionResult Create(ArticleViewModel model)
        {
            if (ModelState.IsValid)
            {
                using (var database = new BlogDbContext())
                {
                    //Get author id
                    var authorId = database.Users
                        .First(u => u.UserName == this.User.Identity.Name)
                        .Id;

                    var article = new Article(authorId, model.Title, model.Content, model.CategoryId);
                    this.SetArticleTags(article, model, database);

                    //Save article in Db
                    database.Articles.Add(article);
                    database.SaveChanges();
                    this.AddNotification("Article created!", NotificationType.INFO);

                    return RedirectToAction("Index");
                }
            }

            return View(model);
        }

        //
        //GET: Articles/Delete
        public ActionResult Delete(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadGateway);
            }

            using (var database = new BlogDbContext())
            {
                //Get article from databse
                var article = database.Articles
                    .Where(a => a.Id == id)
                    .Include(a => a.Author)
                    .Include(a => a.Category)
                    .First();

                if (!IsUserAuthorizedToEdit(article))
                {
                    return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
                }

                ViewBag.TagsString = string.Join(", ", article.Tags.Select(t => t.Name));

                //Check if article exists
                if (article == null)
                {
                    return HttpNotFound();
                }

                this.AddNotification("Article data will be lost!", NotificationType.WARNING);

                //Pass article to view
                return View(article);
            }
        }

        //
        //POST: Articles/Delete
        [HttpPost]
        [ActionName("Delete")]
        public ActionResult DeleteConfirmed(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadGateway);
            }

            using (var database = new BlogDbContext())
            {
                //Get article from db
                var article = database.Articles
                    .Where(a => a.Id == id)
                    .Include(a => a.Author)
                    .First();

                //Check if article exists
                if (article == null)
                {
                    return HttpNotFound();
                }

                //Remove article from db
                database.Articles.Remove(article);
                database.SaveChanges();
                this.AddNotification("Article deleted!", NotificationType.SUCCESS);

                //Redirect to index page
                return RedirectToAction("Index");
            }
        }

        //
        //GET: Article/Edit
        public ActionResult Edit(int? id)
        {
            if (id == null)
            {
                return new HttpStatusCodeResult(HttpStatusCode.BadGateway);
            }

            using (var database = new BlogDbContext())
            {
                //Get article from database
                var article = database.Articles
                    .Where(a => a.Id == id)
                    .First();

                if (!IsUserAuthorizedToEdit(article))
                {
                    return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
                }

                //Check if article exists
                if (article == null)
                {
                    return HttpNotFound();
                }

                //Create the view model
                var model = new ArticleViewModel();
                model.Id = article.Id;
                model.Title = article.Title;
                model.Content = article.Content;
                model.CategoryId = article.CategoryId;
                model.Categories = database.Categories
                    .OrderBy(c => c.Name)
                    .ToList();

                model.Tags = string.Join(", ", article.Tags.Select(t => t.Name));

                //Pass the view model to view
                return View(model);
            }
        }

        //
        //POST: Article/Edit
        [HttpPost]
        public ActionResult Edit(ArticleViewModel model)
        {
            //Check if model state is valid
            if (ModelState.IsValid)
            {
                using (var database = new BlogDbContext())
                {
                    //Get article from database
                    var article = database.Articles
                        .FirstOrDefault(a => a.Id == model.Id);

                    //Set article properties
                    article.Title = model.Title;
                    article.Content = model.Content;
                    article.CategoryId = model.CategoryId;
                    this.SetArticleTags(article, model, database);

                    //Save article state in database
                    database.Entry(article).State = EntityState.Modified;
                    database.SaveChanges();
                    this.AddNotification("Article edited!", NotificationType.SUCCESS);

                    //Return to the index page
                    return RedirectToAction("Index");
                }
            }

            //If model state is invalid return the same view
            return View(model);
        }

        private void SetArticleTags(Article article, ArticleViewModel model, BlogDbContext database)
        {
            //Split tags
            var tagsString = model.Tags
                .Split(new[] { ',', ' ' }, StringSplitOptions.RemoveEmptyEntries)
                .Select(t => t.ToLower())
                .Distinct();

            //Clear current article tags
            article.Tags.Clear();

            //Set new article tags
            foreach (var tagString in tagsString)
            {
                //Get tag from db by its name
                Tag tag = database.Tags
                    .FirstOrDefault(t => t.Name.Equals(tagString));

                //If the tag is null, create new tag
                if (tag == null)
                {
                    tag = new Tag() { Name = tagString };
                    database.Tags.Add(tag);
                }

                //Add tag to article tags
                article.Tags.Add(tag);
            }
        }

        private bool IsUserAuthorizedToEdit(Article article)
        {
            bool isAdmin = this.User.IsInRole("Admin");
            bool isAuthor = article.IsAuthor(this.User.Identity.Name);

            return isAdmin || isAuthor;
        }
    }
}
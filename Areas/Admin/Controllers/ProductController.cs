using BLL.Models;
using BLL.Repositories;
using BLL.ViewModels;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.CodeAnalysis.Operations;
using Microsoft.DotNet.Scaffolding.Shared.Messaging;
using NuGet.Protocol.Plugins;
using System.IO;

namespace PL.Areas.Admin.Controllers
{
    [Area("Admin")]
    public class ProductController : Controller
    {
        private readonly IUnitOfWork _unitOfWork;
        private readonly IWebHostEnvironment _webHostEnvironment;
		public ProductController(IUnitOfWork unitOfWork , IWebHostEnvironment webHostEnvironment)
        {
            _unitOfWork = unitOfWork;
            _webHostEnvironment = webHostEnvironment;
        }
        public IActionResult Index()
        {
            return View();
        }
		public IActionResult GetData()
		{
            var Products = _unitOfWork.Product.GetAll(IncludeWord:"Category");
			return Json(new { data = Products });
		}
		[HttpGet]
        public IActionResult Create()
        {
            ProductVM productVM = new ProductVM()
            {
                Product = new Product(),
                CategoryList = _unitOfWork.Category.GetAll()
                .Select(c => new SelectListItem
                {
                    Text = c.Name,
                    Value = c.Id.ToString()
                }
                )
            };
            return View(productVM);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
		//file => the same name in the name = "file" for image
		public IActionResult Create(ProductVM productVM , IFormFile file)
        {
            if (ModelState.IsValid)
            {
                // to save image in path
                string RootPath = _webHostEnvironment.WebRootPath; // WWWroot
                if (file != null)
                {
                    string filename = Guid.NewGuid().ToString(); // anyName.jpg to this image   
					var upload = Path.Combine(RootPath, @"Images\Products\"); // Full Path
																			 //var upload = Path.Combine(RootPath, "Images", "Products"); //or

					// Ensure directory exists
					if (!Directory.Exists(upload))
					{
						Directory.CreateDirectory(upload);
					}
					
					var ext = Path.GetExtension(file.FileName);
					using (var fileStream = new FileStream(Path.Combine(upload , filename + ext),FileMode.Create))
                    {
						file.CopyTo(fileStream); // put image in this path
					}

                    productVM.Product.Img = @"Images\Products\" + filename + ext;
					//productVM.Product.Img = Path.Combine("Images", "Products", filename + ext); // or
				}
                _unitOfWork.Product.Add(productVM.Product);
                _unitOfWork.Complete();
                TempData["Create"] = "Data Has Created Successfully";
                return RedirectToAction(nameof(Index));
            }
            return View(productVM);
        }

        [HttpGet]
        public IActionResult Edit(int? Id)
        {
            if (Id == null || Id == 0)
            {
                NotFound();
            }
            ProductVM productVM = new ProductVM()
            {
                Product = _unitOfWork.Product.Get(x => x.Id == Id),
                CategoryList = _unitOfWork.Category.GetAll()
                .Select(c => new SelectListItem
                {
                    Text = c.Name,
                    Value = c.Id.ToString()
                }
                )
            };
            return View(productVM);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
		public IActionResult Edit(ProductVM productVM, IFormFile? file)
		{
			if (ModelState.IsValid)
			{
				string rootPath = _webHostEnvironment.WebRootPath;

				if (file != null)
				{
					string fileName = Guid.NewGuid().ToString();
					string uploadPath = Path.Combine(rootPath, @"Images\Products\");

					if (!Directory.Exists(uploadPath))
					{
						Directory.CreateDirectory(uploadPath);
					}

					// Delete the old image if it exists
					if (!string.IsNullOrEmpty(productVM.Product.Img))
					{
						string oldImgPath = Path.Combine(rootPath, productVM.Product.Img.TrimStart('\\'));
						if (System.IO.File.Exists(oldImgPath))
						{
							System.IO.File.Delete(oldImgPath);
						}
					}

					// Save the new image
					string fileExtension = Path.GetExtension(file.FileName);
					string newFilePath = Path.Combine(uploadPath, fileName + fileExtension);

					using (var fileStream = new FileStream(newFilePath, FileMode.Create))
					{
						file.CopyTo(fileStream);
					}

					productVM.Product.Img = @"Images\Products\" + fileName + fileExtension;
				}

				_unitOfWork.Product.Update(productVM.Product);
				_unitOfWork.Complete();
				TempData["Update"] = "Data Has Updated Successfully";
				return RedirectToAction(nameof(Index));
			}

			return View(productVM);
		}

        [HttpDelete]
        public IActionResult DeleteProduct(int? Id)
        {
            var productInDb = _unitOfWork.Product.Get(x => x.Id == Id);
            if (productInDb == null)
            {
                return Json(new {success = false,message="Error while Deleting"});
            }
			_unitOfWork.Product.Remove(productInDb);

            // عشان يحذف الصورة 
			string oldImgPath = Path.Combine(_webHostEnvironment.WebRootPath , productInDb.Img.TrimStart('\\'));
			if (System.IO.File.Exists(oldImgPath))
			{
				System.IO.File.Delete(oldImgPath);
			}
			_unitOfWork.Complete();
            return Json(new { success = true, message = "file has been Deleted" });
        }

    }
}

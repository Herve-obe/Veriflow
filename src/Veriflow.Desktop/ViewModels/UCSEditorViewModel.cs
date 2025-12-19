using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using Veriflow.Desktop.Models;
using Veriflow.Desktop.Services;

namespace Veriflow.Desktop.ViewModels
{
    public partial class UCSEditorViewModel : ObservableObject
    {
        private readonly IUCSService _ucsService;
        
        [ObservableProperty] private ObservableCollection<string> _mainCategories = new();
        [ObservableProperty] private ObservableCollection<UCSCategory> _subCategories = new();
        
        [ObservableProperty] private string? _selectedMainCategory;
        [ObservableProperty] private UCSCategory? _selectedSubCategory;
        
        [ObservableProperty] private string _catID = "";
        [ObservableProperty] private string _fullPath = "";

        public bool DialogResult { get; private set; }

        public UCSEditorViewModel(IUCSService ucsService)
        {
            _ucsService = ucsService;
            LoadCategories();
        }

        public UCSEditorViewModel(IUCSService ucsService, string currentCategory, string currentSubCategory, string currentCatID) 
            : this(ucsService)
        {
            // Pre-select current values
            if (!string.IsNullOrEmpty(currentCategory))
            {
                SelectedMainCategory = currentCategory;
                
                if (!string.IsNullOrEmpty(currentSubCategory))
                {
                    var subCat = SubCategories.FirstOrDefault(s => 
                        s.SubCategory.Equals(currentSubCategory, System.StringComparison.OrdinalIgnoreCase));
                    if (subCat != null)
                    {
                        SelectedSubCategory = subCat;
                    }
                }
            }
        }

        private void LoadCategories()
        {
            var categories = _ucsService.GetMainCategories();
            MainCategories = new ObservableCollection<string>(categories);
        }

        partial void OnSelectedMainCategoryChanged(string? value)
        {
            if (string.IsNullOrEmpty(value))
            {
                SubCategories.Clear();
                SelectedSubCategory = null;
                return;
            }

            var subCats = _ucsService.GetSubCategories(value);
            SubCategories = new ObservableCollection<UCSCategory>(subCats);
            
            // Clear selection when category changes
            SelectedSubCategory = null;
        }

        partial void OnSelectedSubCategoryChanged(UCSCategory? value)
        {
            if (value != null)
            {
                CatID = value.CatID;
                FullPath = value.FullPath;
            }
            else
            {
                CatID = "";
                FullPath = "";
            }
        }

        [RelayCommand]
        private void Ok()
        {
            DialogResult = true;
        }

        [RelayCommand]
        private void Cancel()
        {
            DialogResult = false;
        }

        [RelayCommand]
        private void Clear()
        {
            SelectedMainCategory = null;
            SelectedSubCategory = null;
            CatID = "";
            FullPath = "";
        }
    }
}

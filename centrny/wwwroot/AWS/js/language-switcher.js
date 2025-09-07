// Language Switcher JavaScript
class LanguageSwitcher {
	constructor() {
		this.currentLang = localStorage.getItem("selectedLanguage") || "ar";
		this.init();
	}

	init() {
		this.setLanguage(this.currentLang);
		this.bindEvents();
	}

	bindEvents() {
		// Language switcher button event
		document.addEventListener("click", (e) => {
			if (e.target.closest(".language-switcher")) {
				e.preventDefault();
				this.toggleLanguage();
			}
		});
	}

	toggleLanguage() {
		const newLang = this.currentLang === "ar" ? "en" : "ar";
		this.setLanguage(newLang);
	}

	setLanguage(lang) {
		this.currentLang = lang;
		localStorage.setItem("selectedLanguage", lang);

		// Update HTML attributes
		document.documentElement.lang = lang;
		document.documentElement.dir = lang === "ar" ? "rtl" : "ltr";

		// Update body class for language-specific styling
		document.body.classList.remove("lang-ar", "lang-en");
		document.body.classList.add(`lang-${lang}`);

		// Update all text content
		this.updateContent(lang);

		// Update language switcher icon
		this.updateLanguageSwitcherIcon(lang);

		// Update page-specific content
		this.updatePageContent(lang);

		// Dispatch a custom event for other scripts to listen to
		document.dispatchEvent(
			new CustomEvent("languageChanged", {
				detail: { language: lang, direction: lang === "ar" ? "rtl" : "ltr" },
			})
		);
	}

	updateLanguageSwitcherIcon(lang) {
		const currentLanguageSpan = document.getElementById("current-language");
		if (currentLanguageSpan) {
			currentLanguageSpan.textContent = lang === "ar" ? "العربية" : "English";
		}

		// Also update any other language switcher text elements
		const langTexts = document.querySelectorAll(".lang-text");
		langTexts.forEach((text) => {
			text.textContent = lang === "ar" ? "العربية" : "English";
		});
	}

	updateContent(lang) {
		// Get all elements with data-lang attributes
		const elements = document.querySelectorAll(
			"[data-lang-en], [data-lang-ar]"
		);

		elements.forEach((element) => {
			const langAttr = `data-lang-${lang}`;
			if (element.hasAttribute(langAttr)) {
				const content = element.getAttribute(langAttr);
				if (element.tagName === "INPUT" || element.tagName === "TEXTAREA") {
					element.placeholder = content;
				} else {
					element.innerHTML = content;
				}
			}
		});

		// Update page title
		const titleElement = document.querySelector("title");
		if (titleElement) {
			const titleAr = titleElement.getAttribute("data-lang-ar");
			const titleEn = titleElement.getAttribute("data-lang-en");
			if (lang === "ar" && titleAr) {
				titleElement.textContent = titleAr;
			} else if (lang === "en" && titleEn) {
				titleElement.textContent = titleEn;
			} else {
				// Fallback titles for pages without data attributes
				const pageName = this.getPageName();
				if (lang === "ar") {
					titleElement.textContent = this.getArabicTitle(pageName);
				} else {
					titleElement.textContent = this.getEnglishTitle(pageName);
				}
			}
		}

		// Update navigation menu alignment
		const navItems = document.querySelectorAll(".menu__nav a");
		navItems.forEach((item) => {
			if (lang === "ar") {
				item.style.textAlign = "right";
			} else {
				item.style.textAlign = "left";
			}
		});
	}

	getPageName() {
		const path = window.location.pathname;
		const page = path.split("/").pop().replace(".html", "") || "home";
		return page;
	}

	getArabicTitle(pageName) {
		const titles = {
			home: "الأستاذ أحمد فضل — الفلسفة والمنطق، علم النفس وعلم الاجتماع",
			about: "عن المدرس — الأستاذ أحمد فضل",
			courses: "الدروس — الأستاذ أحمد فضل",
			centres: "المراكز — الأستاذ أحمد فضل",
			"outstanding students": "الطلاب المتميزون — الأستاذ أحمد فضل",
			login: "تسجيل الدخول — الأستاذ أحمد فضل",
			signup: "إنشاء حساب — الأستاذ أحمد فضل",
		};
		return titles[pageName] || "الأستاذ أحمد فضل";
	}

	getEnglishTitle(pageName) {
		const titles = {
			home: "Mr. Ahmed Fadl — Philosophy & Logic, Psychology & Sociology",
			about: "About — Mr. Ahmed Fadl",
			courses: "Courses — Mr. Ahmed Fadl",
			centres: "Centres — Mr. Ahmed Fadl",
			"outstanding students": "Outstanding Students — Mr. Ahmed Fadl",
			login: "Login — Mr. Ahmed Fadl",
			signup: "Sign Up — Mr. Ahmed Fadl",
		};
		return titles[pageName] || "Mr. Ahmed Fadl";
	}

	updatePageContent(lang) {
		// Page-specific content updates that don't use data attributes
		const pageName = this.getPageName();

		if (pageName === "courses") {
			this.updateCoursesPage(lang);
		} else if (pageName === "centres") {
			this.updateCentresPage(lang);
		} else if (pageName === "outstanding students") {
			this.updateOutstandingStudentsPage(lang);
		}
	}

	updateCoursesPage(lang) {
		// Update any hardcoded course content
		const courseCards = document.querySelectorAll(".course-card");
		courseCards.forEach((card) => {
			const button = card.querySelector("button");
			if (button && !button.hasAttribute("data-lang-ar")) {
				if (lang === "ar") {
					button.textContent = "شاهد الآن";
				} else {
					button.textContent = "Watch Now";
				}
			}
		});
	}

	updateCentresPage(lang) {
		// Update centres page specific content
		// This can be expanded based on the content structure
	}

	updateOutstandingStudentsPage(lang) {
		// Update outstanding students page specific content
		// This can be expanded based on the content structure
	}
}

// Initialize language switcher when DOM is loaded
document.addEventListener("DOMContentLoaded", () => {
	window.languageSwitcher = new LanguageSwitcher();
});

// Simple Language Switcher: switches page URL between Arabic and English versions
class LanguageSwitcher {
	constructor() {
		this.bindEvents();
	}

	bindEvents() {
		document.addEventListener("click", (e) => {
			const btn = e.target.closest(".language-switcher");
			if (btn) {
				e.preventDefault();
				this.switchUrl();
			}
		});
	}

	switchUrl() {
		const path = window.location.pathname;
		const isArabic = path.toLowerCase().endsWith("-ar") || path.toLowerCase().includes("-ar.");
		let newPath;

		if (isArabic) {
			// Remove -ar suffix (before extension or at end)
			newPath = path.replace(/-ar(\.[^\/]*)?$/, "$1");
			if (newPath === "") newPath = "/"; // Fallback for root
		} else {
			// Add -ar before extension or at end
			const match = path.match(/^(.*?)(\.[^\/]*)?$/);
			if (match) {
				const base = match[1];
				const ext = match[2] || "";
				if (base === "/" || base === "") {
					newPath = "/home-ar" + ext;
				} else {
					newPath = base + "-ar" + ext;
				}
			} else {
				newPath = path + "-ar";
			}
		}
		window.location.pathname = newPath;
	}
}

// Initialize on DOM ready
document.addEventListener("DOMContentLoaded", () => {
	new LanguageSwitcher();
});
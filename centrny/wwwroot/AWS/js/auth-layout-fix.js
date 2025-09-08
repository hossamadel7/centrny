/**
 * Auth Layout Fix Script
 * Ensures the login/signup form layout stays consistent when switching languages
 * SPECIFICALLY MAINTAINS THE IMAGE POSITION ON THE LEFT SIDE FOR BOTH LANGUAGES
 */

document.addEventListener("DOMContentLoaded", function () {
	// Function to fix the auth page layout
	function fixAuthLayout(event) {
		console.log("Auth layout fix triggered");

		// Get the current language/direction
		const isRTL = document.documentElement.dir === "rtl";

		// Get the form page elements
		const formPage = document.querySelector(".form-page");
		const imgSection = document.querySelector(".form-page__img");
		const contentSection = document.querySelector(".form-page__content");

		if (!formPage || !imgSection || !contentSection) return;

		// ALWAYS FORCE THE SAME LAYOUT REGARDLESS OF LANGUAGE
		console.log("Forcing consistent layout for both RTL and LTR");

		// Force the container to be LTR so image stays on left
		formPage.style.display = "flex";
		formPage.style.flexDirection = "row";
		formPage.style.direction = "ltr"; // Force container to be LTR

		// Image ALWAYS on the left (order 1)
		imgSection.style.order = "1";
		imgSection.style.flex = "0 0 50%";

		// Form content ALWAYS on the right (order 2)
		contentSection.style.order = "2";
		contentSection.style.flex = "0 0 50%";

		// Fix any transforms on images (prevent mirroring)
		const images = imgSection.querySelectorAll("img, [data-move]");
		images.forEach((img) => {
			img.style.transform = "none";
		});

		// NOW SET THE FORM CONTENT DIRECTION BASED ON LANGUAGE
		if (isRTL) {
			console.log("Applying RTL text alignment to form content");

			// Form content should be RTL for Arabic
			contentSection.style.direction = "rtl";
			contentSection.style.textAlign = "right";

			// Fix form input direction for Arabic
			const inputs = contentSection.querySelectorAll("input, textarea");
			inputs.forEach((input) => {
				input.style.textAlign = "right";
				input.style.direction = "rtl";
			});

			// Fix label alignment for Arabic
			const labels = contentSection.querySelectorAll("label");
			labels.forEach((label) => {
				label.style.textAlign = "right";
				label.style.direction = "rtl";
			});
		} else {
			console.log("Applying LTR text alignment to form content");

			// Form content should be LTR for English
			contentSection.style.direction = "ltr";
			contentSection.style.textAlign = "left";

			// Fix form input direction for English
			const inputs = contentSection.querySelectorAll("input, textarea");
			inputs.forEach((input) => {
				input.style.textAlign = "left";
				input.style.direction = "ltr";
			});

			// Fix label alignment for English
			const labels = contentSection.querySelectorAll("label");
			labels.forEach((label) => {
				label.style.textAlign = "left";
				label.style.direction = "ltr";
			});
		}

		// Fix button text alignment (always center)
		const buttons = contentSection.querySelectorAll(".button");
		buttons.forEach((button) => {
			button.style.textAlign = "center";
		});
	}

	// Run the fix initially after a small delay to ensure DOM is fully loaded
	setTimeout(fixAuthLayout, 100);

	// Re-run whenever the language changes
	document.addEventListener("languageChanged", function (event) {
		console.log("Language changed event detected:", event.detail);
		// Add a small delay to ensure the DOM updates first
		setTimeout(fixAuthLayout, 50);
	});

	// As a fallback, check for attribute changes on the html element
	const observer = new MutationObserver((mutations) => {
		mutations.forEach((mutation) => {
			if (mutation.attributeName === "dir") {
				console.log("Direction attribute changed detected");
				// Add a small delay to ensure the DOM updates first
				setTimeout(fixAuthLayout, 50);
			}
		});
	});

	observer.observe(document.documentElement, { attributes: true });

	// Also run on window resize to fix any responsive layout issues
	window.addEventListener("resize", function () {
		setTimeout(fixAuthLayout, 50);
	});
});

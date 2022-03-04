(function () {
	var siteSearchInput = document.querySelector("input.SiteSearch");
	if (siteSearchInput) {
		var request = new XMLHttpRequest();
		request.open("GET", "/AutoComplete.json", true);
		request.onload = function () {
			if (this.status >= 200 && this.status < 400) {
				autocomplete(siteSearchInput, JSON.parse(this.response));
			}
		};
		request.send();
	}
})();

(function () {
	var codeBlocks = document.querySelectorAll("div.Main div.Content pre");
	for (var i = 0; i < codeBlocks.length; i++) {
		codeBlocks[i].classList.add("prettyprint");
	}
	prettyPrint();
})();

(function () {
	var images = document.querySelectorAll("img");
	function ApplyWideImageClassAsRequired(image) {
		if (image.width > 500) {
			image.classList.add("WideImage");
		}
	}
	for (var i = 0; i < images.length; i++) {
		var image = images[i];
		ApplyWideImageClassAsRequired(image);
		images[i].addEventListener("load", function () { ApplyWideImageClassAsRequired(this); });
	}
})();

(function () {
	// Icon free for personal use as per https://uxwing.com/dark-mode-icon/
	var darkModeImage = "<svg version='1.1' id='Layer_1' xmlns='http://www.w3.org/2000/svg' xmlns:xlink='http://www.w3.org/1999/xlink' x='0px' y='0px' viewBox='0 0 122.88 112.88' style='enable-background:new 0 0 122.88 112.88' xml:space='preserve'><style type='text/css'>.st0{fill-rule:evenodd;clip-rule:evenodd;}</style><g><path class='st0' d='M14.29,0h94.3c7.86,0,14.29,6.43,14.29,14.29v84.3c0,7.86-6.43,14.29-14.29,14.29h-94.3 C6.43,112.88,0,106.45,0,98.59v-84.3C0,6.43,6.43,0,14.29,0L14.29,0z M61.36,24.87c-7.69,4.45-12.87,12.77-12.87,22.3 c0,14.22,11.53,25.75,25.75,25.75c6.75,0,12.9-2.6,17.49-6.85c2.28-2.11,4.89-0.57,4.01,1.94c-4.81,13.82-17.95,23.73-33.41,23.73 c-19.54,0-35.38-15.84-35.38-35.38c0-18.72,14.61-34.1,33.1-35.23C63.47,20.93,64.35,23.14,61.36,24.87L61.36,24.87z'/></g></svg>";

	// Icon free for personal use as per https://uxwing.com/light-mode-icon/
	var lightModeImage = "<svg version='1.1' id='Layer_1' xmlns='http://www.w3.org/2000/svg' xmlns:xlink='http://www.w3.org/1999/xlink' x='0px' y='0px' viewBox='0 0 122.88 112.88' style='enable-background:new 0 0 122.88 112.88' xml:space='preserve'><style type='text/css'>.st0{fill-rule:evenodd;clip-rule:evenodd;}</style><g><path class='st0' d='M14.29,0h94.3c7.86,0,14.29,6.43,14.29,14.29v84.3c0,7.86-6.43,14.29-14.29,14.29h-94.3 C6.43,112.88,0,106.45,0,98.59v-84.3C0,6.43,6.43,0,14.29,0L14.29,0z M43.01,28.18c-0.64-1.1-0.26-2.51,0.84-3.15 c1.1-0.64,2.51-0.26,3.15,0.84L49.97,31c0.64,1.1,0.26,2.51-0.84,3.15c-1.1,0.64-2.51,0.26-3.15-0.84L43.01,28.18L43.01,28.18 L43.01,28.18z M61.44,35.79c5.7,0,10.86,2.31,14.6,6.05c3.73,3.74,6.05,8.9,6.05,14.6c0,5.7-2.31,10.86-6.05,14.6 c-3.74,3.74-8.9,6.05-14.6,6.05c-5.7,0-10.86-2.31-14.6-6.05c-3.73-3.74-6.05-8.9-6.05-14.6c0-5.7,2.31-10.86,6.05-14.6 C50.58,38.1,55.74,35.79,61.44,35.79L61.44,35.79L61.44,35.79z M59.6,22.76c0-1.28,1.04-2.31,2.31-2.31c1.28,0,2.31,1.03,2.31,2.31 v5.92c0,1.28-1.03,2.31-2.31,2.31c-1.28,0-2.31-1.03-2.31-2.31V22.76L59.6,22.76L59.6,22.76z M76.7,26.36 c0.63-1.1,2.04-1.48,3.14-0.85c1.1,0.63,1.48,2.04,0.85,3.14l-2.96,5.13c-0.63,1.1-2.04,1.48-3.14,0.85 c-1.1-0.63-1.48-2.04-0.85-3.14L76.7,26.36L76.7,26.36L76.7,26.36z M89.69,38.01c1.1-0.64,2.51-0.26,3.15,0.84 c0.64,1.1,0.26,2.51-0.84,3.15l-5.13,2.96c-1.1,0.64-2.51,0.26-3.15-0.84c-0.64-1.1-0.26-2.51,0.84-3.15L89.69,38.01L89.69,38.01 L89.69,38.01z M95.12,54.6c1.28,0,2.31,1.04,2.31,2.31c0,1.28-1.03,2.31-2.31,2.31H89.2c-1.28,0-2.31-1.03-2.31-2.31 c0-1.28,1.03-2.31,2.31-2.31H95.12L95.12,54.6L95.12,54.6z M91.52,71.7c1.1,0.63,1.48,2.04,0.85,3.14 c-0.63,1.1-2.04,1.48-3.14,0.85l-5.13-2.96c-1.1-0.63-1.48-2.04-0.85-3.14c0.63-1.1,2.04-1.48,3.14-0.85L91.52,71.7L91.52,71.7 L91.52,71.7z M79.87,84.69c0.64,1.1,0.26,2.51-0.84,3.15c-1.1,0.64-2.51,0.26-3.15-0.84l-2.96-5.13c-0.64-1.1-0.26-2.51,0.84-3.15 c1.1-0.64,2.51-0.26,3.15,0.84L79.87,84.69L79.87,84.69L79.87,84.69z M63.28,90.12c0,1.28-1.04,2.31-2.31,2.31 c-1.28,0-2.31-1.03-2.31-2.31V84.2c0-1.28,1.03-2.31,2.31-2.31c1.28,0,2.31,1.03,2.31,2.31V90.12L63.28,90.12L63.28,90.12z M46.18,86.52c-0.63,1.1-2.04,1.48-3.14,0.85c-1.1-0.63-1.48-2.04-0.85-3.14l2.96-5.13c0.63-1.1,2.04-1.48,3.14-0.85 c1.1,0.63,1.48,2.04,0.85,3.14L46.18,86.52L46.18,86.52L46.18,86.52z M33.19,74.87c-1.1,0.64-2.51,0.26-3.15-0.84 c-0.64-1.1-0.26-2.51,0.84-3.15L36,67.91c1.1-0.64,2.51-0.26,3.15,0.84c0.64,1.1,0.26,2.51-0.84,3.15L33.19,74.87L33.19,74.87 L33.19,74.87z M27.76,58.28c-1.28,0-2.31-1.04-2.31-2.31s1.03-2.31,2.31-2.31h5.92c1.28,0,2.31,1.03,2.31,2.31 s-1.03,2.31-2.31,2.31H27.76L27.76,58.28L27.76,58.28z M31.36,41.18c-1.1-0.63-1.48-2.04-0.85-3.14c0.63-1.1,2.04-1.48,3.14-0.85 l5.13,2.96c1.1,0.63,1.48,2.04,0.85,3.14c-0.63,1.1-2.04,1.48-3.14,0.85L31.36,41.18L31.36,41.18L31.36,41.18z'/></g></svg>";
	
	var darkModeLink = document.createElement("a");
	darkModeLink.href = "#";
	darkModeLink.className = "DarkMode";

	function RecordDarkModeEnabled(enabled) {
		if (enabled) {
			localStorage.setItem(darkModeEnabledLocalStorageKey, "1");
			document.querySelector("html").classList.add(darkModeHtmlWrapperClassName);
			darkModeLink.title = "Enable light mode";
			darkModeLink.innerHTML = lightModeImage;
		}
		else {
			localStorage.removeItem(darkModeEnabledLocalStorageKey);
			document.querySelector("html").classList.remove(darkModeHtmlWrapperClassName);
			darkModeLink.title = "Enable dark mode";
			darkModeLink.innerHTML = darkModeImage;
		}
	}

	// Ensure that the button state (title and image) match the current dark-mode/light-mode configuration
	RecordDarkModeEnabled(IsDarkModeEnabled());

	darkModeLink.onclick = function () {
		RecordDarkModeEnabled(!IsDarkModeEnabled());
		return false;
	};
	document.body.append(darkModeLink);
})();
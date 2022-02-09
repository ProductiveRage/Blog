(function () {
	var siteSearchInput = document.querySelector("input.SiteSearch");
	if (siteSearchInput) {
		var request = new XMLHttpRequest();
		request.open("GET", "/AutoComplete.json", true);
		request.onload = function () {
			if (this.status >= 200 && this.status < 400) {
				autocomplete(siteSearchInput, eval(this.response));
			}
		};
		request.send();
    }

	var codeBlocks = document.querySelectorAll("div.Main div.Content pre");
	for (var i = 0; i < codeBlocks.length; i++) {
		codeBlocks[i].classList.add("prettyprint");
    }
	prettyPrint();

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
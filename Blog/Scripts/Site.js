(function () {
	var $search = $("input.SiteSearch");
	if ($search.length > 0) {
		$.ajax({
			url: "/AutoComplete.json", // TODO: need a better way to reference this file since moving this script out of the master page
			dataType: 'json',
			success: function (data) {
				$search.autocomplete(data);
			}
		});
	}
	var $content = $("div.Main div.Content");
	$content.find("pre").addClass("prettyprint");
	prettyPrint();
	$("img").each(function () { ApplyWideImageClassAsRequired(this) }).load(function () { ApplyWideImageClassAsRequired(this) });
	function ApplyWideImageClassAsRequired(objEleImage) {
		var $image = $(objEleImage);
		if ($image.width() > 500) {
			$image.addClass("WideImage");
		}
	}
})();
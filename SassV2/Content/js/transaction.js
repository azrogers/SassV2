var template = $("#user-template");
var users = [];
var index = 1;

function fixNumbers()
{
	for (var i = 0; i < users.length; i++)
	{
		var u = users[i];
		$(u).find(".user-label").text("User " + (i + 1));
		$(u).attr("id", "user-input-" + (i + 1));
	}

	index = users.length + 1;
}

function addUser()
{
	var newUser = template.clone();
	$(newUser).css("display", "block");
	$(newUser).attr("id", "user-input-" + index);
	$(newUser).find(".user-label").text("User " + index);
	$(newUser).find(".user-remove").click(function ()
	{
		$(newUser).remove();
		var i = users.indexOf(newUser);
		users.splice(i, 1);
		fixNumbers();
	});
	$("#users").append(newUser);
	users.push(newUser);
	index++;
}

function checkSubmit()
{
	return $("input[name=name]").val().trim().length > 0;
}
$(function () {
    $(".start").click(startCrawl);
    $(".stop").click(stopCrawling);
});

function startCrawl() {
    $.ajax({
        url: "/WebService1.asmx/StartCrawling?root=" + $("#root").val(),
    });
}

function stopCrawling() {
    $.ajax({
        url: "/WebService1.asmx/StopCrawling",
    });
}

function loadSuggestions() {
    var data = $("#searchbar").val();
    getTableResults();
    $.ajax({
        type: 'POST',
        url: 'WebService1.asmx/searchPrefix',
        contentType: "application/json; charset=utf-8",
        data: '{ prefix: "' + data + '"}',
        dataType: 'json',
        success: function (msg) {
            $("#results").empty();

            if (data.length > 0) {
                var results = msg.d.split('%');
                var html = "";

                $.each(results, function (index, value) {
                    html = html + value + '<br>';
                });

                $("#results").html(html);
            }
        }
    });
}

function searchFunction() {
    var data = $("#searchbar").val();
    getTableResults();

    $.ajax({
        crossDomain: true,
        contentType: "application/json; charset=utf-8",
        url: "http://ec2-54-187-4-45.us-west-2.compute.amazonaws.com/jsonSearch.php",
        data: { search: data },
        dataType: "jsonp",
        success: function (msg) {
            $("#stats").empty();

            $("#stats").html(JSON.stringify(msg[0]));
        }
    });
}

function getTableResults() {
    var data = $("#searchbar").val();

    $.ajax({
        type: 'POST',
        url: 'WebService1.asmx/SearchTable',
        contentType: "application/json; charset=utf-8",
        data: '{ searchString: "' + data + '"}',
        dataType: 'json',
        success: function (msg) {
            $("#tableresults").empty();

            if (data.length > 0) {
                var results = msg.d.split('},{');
                var html = "";

                $.each(results, function (index, value) {
                    //var tempString1 = value;
                    //var tempString2 = value;
                    var tempString1 = value.substring(value.indexOf("title") + 8, value.indexOf('"', value.indexOf("title") + 8));
                    var tempString2 = value.substring(value.indexOf("http"), value.indexOf('","Count'));
                    //html = html + value + "<br>";
                    html = html + tempString1 + '<br> <a href="' + tempString2 + '">Go to this site.</a><br><br>';
                });

                $("#tableresults").html(html);
            }
        }
    });
}

function dashboardStats() {
    $.ajax({
        type: 'POST',
        url: 'WebService1.asmx/getDashboard',
        contentType: "application/json; charset=utf-8",
        dataType: 'json',
        success: function (msg) {
            $("#dashboard").empty();

            var html = "" + msg.d;

            $("#dashboard").html(html);
        }
    });
}

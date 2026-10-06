mergeInto(LibraryManager.library, {
    ShardyWebGLTestSetResult: function (jsonPointer) {
        var report = JSON.parse(UTF8ToString(jsonPointer));
        window.__shardyWebGLTestResult = report;
        document.title = "Shardy WebGL Tests: " + report.status;
        console.log("[Shardy WebGL integration]", report);
    }
});

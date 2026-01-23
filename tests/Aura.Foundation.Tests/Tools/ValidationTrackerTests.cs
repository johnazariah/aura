// <copyright file="ValidationTrackerTests.cs" company="Aura">
// Copyright (c) Aura. All rights reserved.
// </copyright>

namespace Aura.Foundation.Tests.Tools;

using Aura.Foundation.Tools;
using FluentAssertions;
using Xunit;

public class ValidationTrackerTests
{
    [Fact]
    public void NewTracker_HasNoUnvalidatedChanges()
    {
        var tracker = new ValidationTracker();

        tracker.HasUnvalidatedChanges.Should().BeFalse();
        tracker.ModifiedFiles.Should().BeEmpty();
        tracker.ConsecutiveFailures.Should().Be(0);
    }

    [Fact]
    public void TrackFileChange_WithCodeFile_AddsToModifiedFiles()
    {
        var tracker = new ValidationTracker();

        tracker.TrackFileChange("src/Services/OrderService.cs");

        tracker.HasUnvalidatedChanges.Should().BeTrue();
        tracker.ModifiedFiles.Should().Contain("src/Services/OrderService.cs");
    }

    [Fact]
    public void TrackFileChange_WithNonCodeFile_DoesNotTrack()
    {
        var tracker = new ValidationTracker();

        tracker.TrackFileChange("README.md");
        tracker.TrackFileChange("config.json");
        tracker.TrackFileChange("styles.css");

        tracker.HasUnvalidatedChanges.Should().BeFalse();
        tracker.ModifiedFiles.Should().BeEmpty();
    }

    [Theory]
    [InlineData("file.cs")]
    [InlineData("file.ts")]
    [InlineData("file.tsx")]
    [InlineData("file.py")]
    [InlineData("file.go")]
    [InlineData("file.rs")]
    [InlineData("file.java")]
    [InlineData("file.kt")]
    [InlineData("file.swift")]
    [InlineData("file.cpp")]
    [InlineData("file.c")]
    [InlineData("file.h")]
    [InlineData("file.fs")]
    [InlineData("file.rb")]
    [InlineData("file.js")]
    [InlineData("file.jsx")]
    public void TrackFileChange_WithVariousCodeExtensions_TracksAll(string fileName)
    {
        var tracker = new ValidationTracker();

        tracker.TrackFileChange(fileName);

        tracker.HasUnvalidatedChanges.Should().BeTrue();
        tracker.ModifiedFiles.Should().Contain(fileName);
    }

    [Fact]
    public void RecordValidationResult_Success_ClearsModifiedFiles()
    {
        var tracker = new ValidationTracker();
        tracker.TrackFileChange("src/A.cs");
        tracker.TrackFileChange("src/B.cs");

        tracker.RecordValidationResult(success: true);

        tracker.HasUnvalidatedChanges.Should().BeFalse();
        tracker.ModifiedFiles.Should().BeEmpty();
        tracker.ConsecutiveFailures.Should().Be(0);
    }

    [Fact]
    public void RecordValidationResult_Failure_IncrementsConsecutiveFailures()
    {
        var tracker = new ValidationTracker();
        tracker.TrackFileChange("src/A.cs");

        tracker.RecordValidationResult(success: false);

        tracker.ConsecutiveFailures.Should().Be(1);
        tracker.HasUnvalidatedChanges.Should().BeTrue(); // Files still tracked
    }

    [Fact]
    public void RecordValidationResult_MultipleFailures_AccumulatesCount()
    {
        var tracker = new ValidationTracker();
        tracker.TrackFileChange("src/A.cs");

        tracker.RecordValidationResult(success: false);
        tracker.RecordValidationResult(success: false);
        tracker.RecordValidationResult(success: false);

        tracker.ConsecutiveFailures.Should().Be(3);
    }

    [Fact]
    public void RecordValidationResult_SuccessAfterFailures_ResetsCount()
    {
        var tracker = new ValidationTracker();
        tracker.TrackFileChange("src/A.cs");
        tracker.RecordValidationResult(success: false);
        tracker.RecordValidationResult(success: false);

        tracker.RecordValidationResult(success: true);

        tracker.ConsecutiveFailures.Should().Be(0);
    }

    [Fact]
    public void MaxFailures_DefaultValue_IsFive()
    {
        var tracker = new ValidationTracker();

        tracker.MaxFailures.Should().Be(5);
    }

    [Fact]
    public void MaxFailures_CanBeCustomized()
    {
        var tracker = new ValidationTracker { MaxFailures = 3 };

        tracker.MaxFailures.Should().Be(3);
    }

    [Fact]
    public void TrackFileChange_DuplicatePaths_OnlyTracksOnce()
    {
        var tracker = new ValidationTracker();

        tracker.TrackFileChange("src/A.cs");
        tracker.TrackFileChange("src/A.cs");
        tracker.TrackFileChange("src/A.cs");

        tracker.ModifiedFiles.Should().HaveCount(1);
    }

    [Fact]
    public void TrackFileChange_NullOrEmptyPath_DoesNotThrow()
    {
        var tracker = new ValidationTracker();

        var act1 = () => tracker.TrackFileChange(null!);
        var act2 = () => tracker.TrackFileChange("");
        var act3 = () => tracker.TrackFileChange("   ");

        act1.Should().NotThrow();
        act2.Should().NotThrow();
        act3.Should().NotThrow();
        tracker.HasUnvalidatedChanges.Should().BeFalse();
    }

    [Fact]
    public void TrackFileChange_CaseInsensitiveExtension_TracksCorrectly()
    {
        var tracker = new ValidationTracker();

        tracker.TrackFileChange("File.CS");
        tracker.TrackFileChange("File.Ts");
        tracker.TrackFileChange("File.PY");

        tracker.ModifiedFiles.Should().HaveCount(3);
    }
}

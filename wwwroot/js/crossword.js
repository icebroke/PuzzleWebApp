(() => {
  const root = document.querySelector("#crossword-game");
  if (!root) {
    return;
  }

  const inputs = Array.from(root.querySelectorAll(".crossword-cell"));
  const clueButtons = Array.from(root.querySelectorAll(".clue-button"));
  const directionToggle = root.querySelector("#direction-toggle");
  const directionLabel = root.querySelector("#direction-label");
  const saveStatus = root.querySelector("#save-status");
  const progressLabel = root.querySelector("#progress-label");
  const submitButton = root.querySelector("#submit-puzzle");
  const clearButton = root.querySelector("#clear-answers");
  const submissionBanner = root.querySelector("#submission-banner");
  const submissionFeedback = root.querySelector("#submission-feedback");
  const puzzleTimer = root.querySelector("#puzzle-timer");
  const remainingTime = root.querySelector("#remaining-time");
  const creativeRoot = root.querySelector("#creative-question");
  const creativeAnswer = root.querySelector("#creative-answer");
  const creativeSubmitButton = root.querySelector("#submit-creative-answer");
  const creativeSaveStatus = root.querySelector("#creative-save-status");
  const creativeFeedback = root.querySelector("#creative-feedback");
  const token = root.querySelector(
    'input[name="__RequestVerificationToken"]',
  ).value;
  const cluesByKey = new Map(
    clueButtons.map((button) => [button.dataset.clueKey, button]),
  );
  let activeDirection = "Across";
  let activeKey = "";
  let activeInput = null;
  let saveTimer;
  let countdownTimer;
  let saveQueue = Promise.resolve();
  let creativeSaveQueue = Promise.resolve();
  let creativeSaveTimer;
  let creativeCountdownTimer;

  const keyFor = (input, direction) =>
    input.dataset[`${direction.toLowerCase()}Key`];
  const directionName = (direction) =>
    direction === "Across" ? "Melintang" : "Menegak";

  function selectClue(key) {
    const clue = cluesByKey.get(key);
    if (!clue) {
      return;
    }

    activeKey = key;
    activeDirection = clue.dataset.direction;
    directionLabel.textContent = directionName(activeDirection);
    directionToggle.setAttribute(
      "aria-label",
      `Tukar arah petunjuk, kini ${directionName(activeDirection)}`,
    );
    clueButtons.forEach((button) => {
      const selected = button === clue;
      button.classList.toggle("is-active", selected);
      button.setAttribute("aria-pressed", String(selected));
    });
    inputs.forEach((input) => {
      const selected = keyFor(input, activeDirection) === activeKey;
      input.classList.toggle("in-active-word", selected);
      input.classList.toggle(
        "is-selected",
        selected && document.activeElement === input,
      );
    });
  }

  function chooseClueForCell(input, preferToggle = false) {
    const acrossKey = keyFor(input, "Across");
    const downKey = keyFor(input, "Down");
    let nextKey;

    if (acrossKey && downKey) {
      if (preferToggle && activeKey === acrossKey) {
        nextKey = downKey;
      } else if (preferToggle && activeKey === downKey) {
        nextKey = acrossKey;
      } else if (activeKey === acrossKey || activeKey === downKey) {
        nextKey = activeKey;
      } else {
        nextKey = activeDirection === "Down" ? downKey : acrossKey;
      }
    } else {
      nextKey = acrossKey || downKey;
    }

    if (nextKey) {
      selectClue(nextKey);
    }
  }

  function moveWithinClue(input, offset) {
    const clue = cluesByKey.get(activeKey);
    if (!clue) {
      return;
    }

    const startRow = Number(clue.dataset.startRow);
    const startColumn = Number(clue.dataset.startColumn);
    const currentRow = Number(input.dataset.row);
    const currentColumn = Number(input.dataset.column);
    const index =
      activeDirection === "Across"
        ? currentColumn - startColumn
        : currentRow - startRow;
    const targetIndex = index + offset;
    const length = Number(clue.dataset.length);
    if (targetIndex < 0 || targetIndex >= length) {
      return;
    }

    const row = startRow + (activeDirection === "Down" ? targetIndex : 0);
    const column =
      startColumn + (activeDirection === "Across" ? targetIndex : 0);
    const target = root.querySelector(
      `.crossword-cell[data-row="${row}"][data-column="${column}"]`,
    );
    target?.focus();
  }

  function moveByArrow(input, key) {
    const offsets = {
      ArrowLeft: [0, -1],
      ArrowRight: [0, 1],
      ArrowUp: [-1, 0],
      ArrowDown: [1, 0],
    };
    const [rowOffset, columnOffset] = offsets[key];
    const row = Number(input.dataset.row) + rowOffset;
    const column = Number(input.dataset.column) + columnOffset;
    const target = root.querySelector(
      `.crossword-cell[data-row="${row}"][data-column="${column}"]`,
    );
    target?.focus();
  }

  function getLetters() {
    const letters = {};
    inputs.forEach((input) => {
      const letter = input.value.trim().toUpperCase();
      if (letter) {
        letters[`${input.dataset.row},${input.dataset.column}`] = letter;
      }
    });
    return letters;
  }

  function updateAnsweredCount(answeredCount, totalClues) {
    progressLabel.textContent = `${answeredCount} daripada ${totalClues} petunjuk dijawab`;
  }

  async function sendProgress(snapshot) {
    saveStatus.textContent = "Menyimpan kemajuan…";
    const response = await fetch("/Home/SaveProgress", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        RequestVerificationToken: token,
      },
      body: JSON.stringify({ letters: snapshot }),
    });
    let result;
    try {
      result = await response.json();
    } catch {
      throw new Error(
        "Pelayan memulangkan respons simpanan yang tidak dapat dibaca.",
      );
    }

    if (response.status === 429) {
      window.location.assign(result.redirectUrl || "/Home/RateLimitError");
      throw new Error(
        result.message ||
          "Terlalu banyak permintaan dibuat. Sila tunggu sebentar.",
      );
    }

    if (!response.ok) {
      throw new Error(result.message || "Data tidak dapat disimpan.");
    }

    updateAnsweredCount(result.answeredCount, result.totalClues);
    saveStatus.textContent = "Kemajuan disimpan";
  }

  function saveProgress(snapshot = getLetters()) {
    const saveOperation = saveQueue.then(() => sendProgress(snapshot));
    saveQueue = saveOperation.catch((error) => {
      saveStatus.textContent =
        error.message || "Kemajuan tidak dapat disimpan. Sila cuba lagi.";
      saveStatus.closest(".game-status").classList.add("has-error");
    });
    saveStatus.closest(".game-status").classList.remove("has-error");
    return saveOperation;
  }

  function scheduleSave() {
    window.clearTimeout(saveTimer);
    saveTimer = window.setTimeout(() => saveProgress().catch(() => {}), 350);
    saveStatus.textContent = "Perubahan belum disimpan";
  }

  function saveCreativeAnswer() {
    const snapshot = creativeAnswer.value;
    const operation = creativeSaveQueue.then(async () => {
      creativeSaveStatus.textContent = "Menyimpan jawapan…";
      const response = await fetch("/Home/SaveCreativeProgress", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          RequestVerificationToken: token,
        },
        body: JSON.stringify({ answer: snapshot }),
      });
      const result = await response.json();
      if (response.status === 429) {
        window.location.assign(result.redirectUrl || "/Home/RateLimitError");
        throw new Error(result.message || "Terlalu banyak permintaan dibuat.");
      }
      if (!response.ok) {
        throw new Error(result.message || "Jawapan tidak dapat disimpan.");
      }
      creativeSaveStatus.textContent = "Jawapan disimpan";
    });
    creativeSaveQueue = operation.catch((error) => {
      creativeSaveStatus.textContent =
        error.message || "Jawapan tidak dapat disimpan.";
    });
    return operation;
  }

  function scheduleCreativeSave() {
    window.clearTimeout(creativeSaveTimer);
    creativeSaveStatus.textContent = "Perubahan belum disimpan";
    creativeSaveTimer = window.setTimeout(() => {
      saveCreativeAnswer().catch(() => {});
    }, 500);
  }

  async function submitCreativeAnswer(timedOut = false) {
    if (!creativeAnswer || creativeRoot.dataset.submitting === "true") {
      return;
    }
    creativeRoot.dataset.submitting = "true";
    window.clearTimeout(creativeSaveTimer);
    window.clearInterval(creativeCountdownTimer);
    creativeAnswer.disabled = true;
    if (creativeSubmitButton) {
      window.AppUi.setButtonLoading(creativeSubmitButton, true);
    }
    creativeSaveStatus.textContent = timedOut
      ? "Masa tamat. Menghantar jawapan…"
      : "Menghantar jawapan…";
    creativeFeedback.textContent = "";
    try {
      await creativeSaveQueue;
      const response = await fetch("/Home/SubmitCreativeAnswer", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          RequestVerificationToken: token,
        },
        body: JSON.stringify({ answer: creativeAnswer.value }),
      });
      const result = await response.json();
      if (response.status === 429) {
        window.location.assign(result.redirectUrl || "/Home/RateLimitError");
        return;
      }
      if (!response.ok) {
        throw new Error(result.message || "Jawapan tidak dapat dihantar.");
      }
      window.location.reload();
    } catch (error) {
      creativeRoot.dataset.submitting = "false";
      creativeAnswer.disabled = false;
      if (creativeSubmitButton) {
        window.AppUi.setButtonLoading(creativeSubmitButton, false);
      }
      creativeSaveStatus.textContent = "Penghantaran tidak berjaya";
      creativeFeedback.textContent =
        error.message || "Jawapan tidak dapat dihantar.";
      creativeFeedback.classList.add("has-error");
      if (timedOut) {
        window.setTimeout(() => submitCreativeAnswer(true), 5000);
      }
    }
  }

  if (creativeRoot && creativeAnswer) {
    creativeAnswer.addEventListener("input", scheduleCreativeSave);
    creativeSubmitButton?.addEventListener("click", () => {
      if (
        window.confirm(
          "Hantar pesanan anda? Anda tidak boleh mengubahnya selepas dihantar.",
        )
      ) {
        submitCreativeAnswer();
      }
    });

    const creativeDeadline =
      Date.now() + Number(creativeRoot.dataset.remainingSeconds) * 1000;
    const updateCreativeCountdown = () => {
      const secondsLeft = Math.max(
        0,
        Math.ceil((creativeDeadline - Date.now()) / 1000),
      );
      root.querySelector("#creative-remaining-time").textContent =
        `${Math.floor(secondsLeft / 60)}:${String(secondsLeft % 60).padStart(2, "0")}`;
      root
        .querySelector("#creative-timer")
        ?.classList.toggle("is-urgent", secondsLeft > 0 && secondsLeft <= 60);
      if (secondsLeft === 0) {
        submitCreativeAnswer(true);
      }
    };
    updateCreativeCountdown();
    creativeCountdownTimer = window.setInterval(updateCreativeCountdown, 250);
  }

  function submitTimedOutPuzzle() {
    if (!puzzleTimer || puzzleTimer.dataset.running !== "true") {
      return;
    }

    if (puzzleTimer.dataset.submitting === "true") {
      return;
    }
    puzzleTimer.dataset.submitting = "true";
    window.clearTimeout(saveTimer);
    window.clearInterval(countdownTimer);
    puzzleTimer.classList.add("is-expired");
    puzzleTimer.querySelector(".puzzle-timer-label").textContent = "Masa tamat";
    remainingTime.textContent = "00:00";
    inputs.forEach((input) => (input.disabled = true));
    if (clearButton) clearButton.disabled = true;
    if (submitButton) window.AppUi.setButtonLoading(submitButton, true);
    saveStatus.textContent = "Masa tamat. Menghantar jawapan anda…";

    fetch("/Home/Submit", {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        RequestVerificationToken: token,
      },
      body: JSON.stringify({ letters: getLetters() }),
    })
      .then(async (response) => {
        const result = await response.json();
        if (response.status === 429) {
          window.location.assign(result.redirectUrl || "/Home/RateLimitError");
          return;
        }
        if (!response.ok) {
          throw new Error(result.message || "Jawapan tidak dapat dihantar.");
        }
        window.location.reload();
      })
      .catch(() => {
        puzzleTimer.dataset.submitting = "false";
        saveStatus.textContent = "Masa tamat. Menyambung penghantaran…";
        window.setTimeout(submitTimedOutPuzzle, 5000);
      });
  }

  if (puzzleTimer?.dataset.running === "true") {
    const deadline =
      Date.now() + Number(puzzleTimer.dataset.remainingSeconds) * 1000;
    const updateCountdown = () => {
      const secondsLeft = Math.max(
        0,
        Math.ceil((deadline - Date.now()) / 1000),
      );
      remainingTime.textContent = `${Math.floor(secondsLeft / 60)}:${String(secondsLeft % 60).padStart(2, "0")}`;
      puzzleTimer.classList.toggle(
        "is-urgent",
        secondsLeft > 0 && secondsLeft <= 60,
      );
      if (secondsLeft === 0) {
        submitTimedOutPuzzle();
      }
    };
    updateCountdown();
    countdownTimer = window.setInterval(updateCountdown, 250);
  }

  inputs.forEach((input) => {
    input.addEventListener("focus", () => {
      activeInput = input;
      chooseClueForCell(input);
      input.select();
      input.classList.add("is-selected");
    });
    input.addEventListener("input", () => {
      input.value = input.value
        .toUpperCase()
        .replace(/[^A-Z]/g, "")
        .slice(0, 1);
      scheduleSave();
      if (input.value) {
        moveWithinClue(input, 1);
      }
    });
    input.addEventListener("keydown", (event) => {
      if (event.key.startsWith("Arrow")) {
        event.preventDefault();
        moveByArrow(input, event.key);
      } else if (event.key === "Backspace" && !input.value) {
        event.preventDefault();
        moveWithinClue(input, -1);
      } else if (event.key === "Enter") {
        event.preventDefault();
        chooseClueForCell(input, true);
      }
    });
    input.addEventListener("blur", () => input.classList.remove("is-selected"));
  });

  clueButtons.forEach((button) => {
    button.addEventListener("click", () => {
      selectClue(button.dataset.clueKey);
      const row = button.dataset.startRow;
      const column = button.dataset.startColumn;
      root
        .querySelector(
          `.crossword-cell[data-row="${row}"][data-column="${column}"]`,
        )
        ?.focus();
    });
  });

  directionToggle.addEventListener("click", () => {
    const nextDirection = activeDirection === "Across" ? "Down" : "Across";
    const nextKey = activeInput && keyFor(activeInput, nextDirection);
    if (nextKey) {
      selectClue(nextKey);
      activeInput.focus();
      return;
    }

    const nextInput = inputs.find((candidate) =>
      keyFor(candidate, nextDirection),
    );
    if (nextInput) {
      activeInput = nextInput;
      selectClue(keyFor(nextInput, nextDirection));
      nextInput.focus();
    }
  });

  clearButton?.addEventListener("click", async () => {
    if (!window.confirm("Padam semua huruf dalam teka silang kata ini?")) {
      return;
    }

    window.clearTimeout(saveTimer);
    inputs.forEach((input) => {
      input.value = "";
    });
    saveStatus.closest(".game-status").classList.remove("has-error");
    saveStatus.textContent = "Mengosongkan grid dan menyimpan…";
    window.AppUi.setButtonLoading(clearButton, true);
    try {
      await saveProgress();
    } catch {
      saveStatus.textContent =
        "Grid telah dikosongkan, tetapi tidak dapat disimpan. Sila cuba lagi.";
      saveStatus.closest(".game-status").classList.add("has-error");
    } finally {
      window.AppUi.setButtonLoading(clearButton, false);
    }
  });

  submitButton?.addEventListener("click", async () => {
    if (
      !window.confirm(
        "Hantar jawapan anda? Anda tidak boleh mengubahnya selepas dihantar.",
      )
    ) {
      return;
    }

    window.clearTimeout(saveTimer);
    const snapshot = getLetters();
    inputs.forEach((input) => (input.disabled = true));
    window.AppUi.setButtonLoading(submitButton, true);
    saveStatus.textContent = "Menyimpan jawapan sebelum dihantar…";
    if (clearButton) {
      clearButton.disabled = true;
    }
    submissionFeedback.textContent = "";
    submissionFeedback.classList.remove("has-error");

    try {
      await saveProgress(snapshot);
      saveStatus.textContent = "Menghantar jawapan…";
      const response = await fetch("/Home/Submit", {
        method: "POST",
        headers: {
          "Content-Type": "application/json",
          RequestVerificationToken: token,
        },
        body: JSON.stringify({ letters: snapshot }),
      });
      let result;
      try {
        result = await response.json();
      } catch {
        throw new Error(
          "Pelayan memulangkan respons penghantaran yang tidak dapat dibaca.",
        );
      }

      if (response.status === 429) {
        window.location.assign(result.redirectUrl || "/Home/RateLimitError");
        throw new Error(
          result.message ||
            "Terlalu banyak permintaan dibuat. Sila tunggu sebentar.",
        );
      }

      if (!response.ok) {
        throw new Error(result.message || "Jawapan anda tidak dapat dihantar.");
      }

      if (result.creativePhaseStarted) {
        window.location.reload();
        return;
      }

      saveStatus.textContent = "Jawapan telah dihantar";
      window.clearInterval(countdownTimer);
      submissionFeedback.textContent = "";
      submissionBanner.innerHTML =
        '<span class="completion-icon" aria-hidden="true">✓</span><div><strong>Jawapan anda telah dihantar.</strong><span>Terima kasih. Jawapan tidak boleh diubah lagi.</span></div>';
      submissionBanner.classList.remove("d-none");
      submitButton.remove();
      clearButton?.remove();
      document.querySelector(".keyboard-hint").textContent =
        "Jawapan anda telah dihantar dan tidak boleh diubah lagi.";
    } catch (error) {
      inputs.forEach((input) => (input.disabled = false));
      window.AppUi.setButtonLoading(submitButton, false);
      saveStatus.textContent = "Penghantaran tidak berjaya";
      saveStatus.closest(".game-status").classList.add("has-error");
      if (clearButton) {
        clearButton.disabled = false;
      }
      submissionFeedback.textContent =
        error.message || "Penghantaran jawapan tidak berjaya.";
      submissionFeedback.classList.add("has-error");
    }
  });

  const firstInput = inputs[0];
  if (firstInput) {
    activeInput = firstInput;
    chooseClueForCell(firstInput);
  }
})();

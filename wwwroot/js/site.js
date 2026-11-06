(() => {
  function setButtonLoading(button, isLoading) {
    const label = button.querySelector("[data-button-label]");
    const spinner = button.querySelector("[data-button-spinner]");
    if (!label || !spinner) {
      throw new Error("Loading buttons need a label and spinner element.");
    }

    if (isLoading) {
      if (!button.dataset.idleLabel) {
        button.dataset.idleLabel = label.textContent;
      }
      label.textContent = button.dataset.loadingText || "Sedang memproses…";
      spinner.hidden = false;
      button.disabled = true;
      button.setAttribute("aria-busy", "true");
      return;
    }

    label.textContent = button.dataset.idleLabel || label.textContent;
    spinner.hidden = true;
    button.disabled = false;
    button.setAttribute("aria-busy", "false");
  }

  window.AppUi = { setButtonLoading };

  const puzzleForm = document.querySelector("[data-puzzle-form]");
  if (puzzleForm) {
    const clueList = puzzleForm.querySelector("[data-clue-list]");
    const clueTemplate = document.querySelector("#puzzle-clue-template");

    function reindexClues() {
      const rows = [...clueList.querySelectorAll("[data-clue-row]")];
      rows.forEach((row, index) => {
        row.querySelector("legend").innerHTML = `Petunjuk ${index + 1}`;
        const removeButton = row.querySelector("[data-remove-clue]");
        removeButton.setAttribute("aria-label", `Buang petunjuk ${index + 1}`);

        row.querySelectorAll("[data-name]").forEach((field) => {
          const property = field.dataset.name;
          const id = `Clues_${index}__${property}`;
          field.name = `Clues[${index}].${property}`;
          field.id = id;
          row.querySelector(`[data-label='${property}']`).htmlFor = id;
        });

        row.querySelectorAll("input[name^='Clues['], select[name^='Clues[']").forEach((field) => {
          if (field.dataset.name) return;
          const property = field.name.match(/\]\.([A-Za-z]+)/)?.[1];
          if (!property) return;
          const id = `Clues_${index}__${property}`;
          const label = row.querySelector(`label[for='${field.id}']`);
          field.name = `Clues[${index}].${property}`;
          field.id = id;
          if (label) label.htmlFor = id;
        });
      });
    }

    puzzleForm.addEventListener("click", (event) => {
      const addButton = event.target.closest("[data-add-clue]");
      if (addButton) {
        const row = clueTemplate.content.firstElementChild.cloneNode(true);
        const nextNumber = clueList.querySelectorAll("[data-clue-row]").length + 1;
        row.querySelector("[data-name='Number']").value = nextNumber;
        clueList.append(row);
        reindexClues();
        row.querySelector("[data-name='Text']")?.focus();
        return;
      }

      const removeButton = event.target.closest("[data-remove-clue]");
      if (removeButton) {
        removeButton.closest("[data-clue-row]").remove();
        reindexClues();
      }
    });

    if (clueList.children.length === 0) {
      const row = clueTemplate.content.firstElementChild.cloneNode(true);
      clueList.append(row);
    }
    reindexClues();
  }

  document.addEventListener("submit", async (event) => {
    const form = event.target;
    if (
      !(form instanceof HTMLFormElement) ||
      !form.matches("[data-ajax-form]")
    ) {
      return;
    }

    event.preventDefault();
    const button =
      event.submitter ||
      form.querySelector('button[type="submit"], button:not([type])');
    if (!button) {
      return;
    }

    if (button.disabled) {
      return;
    }

    let feedback = form.querySelector(".form-action-feedback");
    if (!feedback) {
      feedback = document.createElement("p");
      feedback.className = "form-action-feedback";
      feedback.setAttribute("role", "status");
      feedback.setAttribute("aria-live", "polite");
      form.append(feedback);
    }
    feedback.hidden = true;
    feedback.textContent = "";
    setButtonLoading(button, true);

    let navigating = false;
    try {
      const response = await fetch(form.action, {
        method: form.method,
        body: new FormData(form),
        credentials: "same-origin",
      });

      if (response.redirected) {
        navigating = true;
        window.location.assign(response.url);
        return;
      }

      const contentType = response.headers.get("content-type") || "";
      if (contentType.includes("text/html")) {
        const documentFromResponse = new DOMParser().parseFromString(
          await response.text(),
          "text/html",
        );
        const nextCard = documentFromResponse.querySelector(".join-card");
        const currentCard = form.closest(".join-card");

        if (response.ok && nextCard && currentCard) {
          currentCard.replaceWith(nextCard);
          document.title = documentFromResponse.title;
          if (
            window.jQuery &&
            window.jQuery.validator &&
            window.jQuery.validator.unobtrusive
          ) {
            window.jQuery.validator.unobtrusive.parse(nextCard);
          }
          nextCard
            .querySelector(
              "input.input-validation-error, input.is-invalid, input[aria-invalid='true']",
            )
            ?.focus();
          return;
        }

        const message =
          documentFromResponse.querySelector(".validation-summary")
            ?.textContent.trim() ||
          documentFromResponse.querySelector('[role="alert"]')
            ?.textContent.trim();
        throw new Error(
          message || "Permintaan tidak berjaya. Sila cuba lagi.",
        );
      }

      if (!response.ok) {
        let message = "Permintaan tidak berjaya. Sila cuba lagi.";
        if (contentType.includes("application/json")) {
          const result = await response.json();
          message = result.message || message;
        }
        throw new Error(message);
      }
    } catch (error) {
      if (feedback) {
        feedback.textContent =
          error instanceof Error
            ? error.message
            : "Permintaan tidak berjaya. Sila cuba lagi.";
        feedback.hidden = false;
      }
    } finally {
      if (!navigating && button.isConnected) {
        setButtonLoading(button, false);
      }
    }
  });
})();

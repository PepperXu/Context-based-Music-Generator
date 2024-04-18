using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using OpenAI;
using System;

public class GPTCommunication : MonoBehaviour
{
    private OpenAIApi openai = new OpenAIApi("");
    private List<ChatMessageWithImage> messages = new List<ChatMessageWithImage>();
    private List<ChatMessage> testMessages = new List<ChatMessage>();
    
    private string sysPrompt;

    // Start is called before the first frame update
    void Start()
    {
        
    }

    // Update is called once per frame
    void Update()
    {
        
    }

    public void GenerateCompletionForImage(string prompt, string base64_image, Action<string> callback)
    {
        SendReplyWithImage(prompt, base64_image, callback);
        
    }

    public void GenerateCompletionForImage(string prompt, string base64_image, int index, Action<string, int> callback)
    {
        SendReplyWithImage(prompt, base64_image, index, callback);
        
    }

    public void GenerateTestCompletion(Action<string> callback){
        SendReply(callback);
    }

    private async void SendReply(Action<string> callback)
        {
            var newMessage = new ChatMessage()
            {
                Role = "user",
                Content = "Hi please introduce your self."
            };
            
            testMessages.Add(newMessage);
            
            DebugText.SetText("Start Generating Completion..");
            // Complete the instruction
            var completionResponse = await openai.CreateChatCompletion(new CreateChatCompletionRequest()
            {
                Model = "gpt-4-vision-preview",
                Messages = testMessages
            });
            DebugText.SetText("Completion Generated.");
            if (completionResponse.Choices != null && completionResponse.Choices.Count > 0)
            {
                var message = completionResponse.Choices[0].Message;
                message.Content = message.Content.Trim();
                
                testMessages.Add(message);
                callback(message.Content);
            }
            else
            {
                 DebugText.SetText("No text was generated from this prompt.");
            }
        }

    private async void SendReplyWithImage(string prompt, string base64_image, Action<string> callback)
    {
        var newMessage = new ChatMessageWithImage()
        {
            Role = "user",
            Content = new List<Content>()
        };
        
        
        newMessage.Content.Add(new Content() {
            Type = "text",
            Text = messages.Count == 0 ? sysPrompt + "\n" + prompt : prompt
        });


        newMessage.Content.Add(new Content(){
            Type = "image_url",
            ImageUrl = new ImageData(){
                Url = "data:image/jpeg;base64,{" + base64_image + "}"
            }
        });

        messages.Add(newMessage);
        
        
        // Complete the instruction
        DebugText.SetText("Start Generating Completion..");
        var completionResponse = await openai.CreateChatCompletion(new CreateChatCompletionRequestWithImage()
        {
            Model = "gpt-4-turbo",
            Messages = messages
        });

        DebugText.SetText("Completion Generated.");


        if (completionResponse.Error != null)
        {
            ApiError error = completionResponse.Error;
            DebugText.SetText($"Error Message: {error.Message}\nError Type: {error.Type}\n");
        }

        if (completionResponse.Choices != null && completionResponse.Choices.Count > 0)
        {
            var message = completionResponse.Choices[0].Message;

            var responseMessage = new ChatMessageWithImage()
            {
                Role = message.Role,
                Content = new List<Content>(){new Content(){Type = "text", Text = message.Content.Trim()}}
            };
            messages.Add(responseMessage);
            callback(responseMessage.Content[0].Text);
        }
        else
        {
            DebugText.SetText("No text was generated from this prompt.");
        }

    }

    private async void SendReplyWithImage(string prompt, string base64_image, int index, Action<string, int> callback)
    {
        var newMessage = new ChatMessageWithImage()
        {
            Role = "user",
            Content = new List<Content>()
        };
        
        
        newMessage.Content.Add(new Content() {
            Type = "text",
            Text = messages.Count == 0 ? sysPrompt + "\n" + prompt : prompt
        });


        newMessage.Content.Add(new Content(){
            Type = "image_url",
            ImageUrl = new ImageData(){
                Url = "data:image/jpeg;base64,{" + base64_image + "}"
            }
        });

        messages.Add(newMessage);
        
        
        // Complete the instruction
        DebugText.SetText("Start Generating Completion..");
        var completionResponse = await openai.CreateChatCompletion(new CreateChatCompletionRequestWithImage()
        {
            Model = "gpt-4-turbo",
            Messages = messages
        });

        DebugText.SetText("Completion Generated.");


        if (completionResponse.Error != null)
        {
            ApiError error = completionResponse.Error;
            DebugText.SetText($"Error Message: {error.Message}\nError Type: {error.Type}\n");
        }

        if (completionResponse.Choices != null && completionResponse.Choices.Count > 0)
        {
            var message = completionResponse.Choices[0].Message;

            var responseMessage = new ChatMessageWithImage()
            {
                Role = message.Role,
                Content = new List<Content>(){new Content(){Type = "text", Text = message.Content.Trim()}}
            };
            messages.Add(responseMessage);
            callback(responseMessage.Content[0].Text, index);
        }
        else
        {
            DebugText.SetText("No text was generated from this prompt.");
        }

    }

    public void ClearHistory(){
        messages.Clear();
        sysPrompt = "";
    }

    public void SetSystemPrompt(string prompt){
        sysPrompt = prompt;
    }
}
